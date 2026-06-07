// SPDX-License-Identifier: MIT
using UnityEngine;
using UnityEngine.Rendering;

namespace GaussianSplatting.Runtime
{
    [ExecuteInEditMode]
    public class GaussianShadowSystem : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("그림자를 만드는 GS (Human 등)")]
        public GaussianSplatRenderer shadowCaster;
        [Tooltip("그림자를 받는 GS (BG 등)")]
        public GaussianSplatRenderer shadowReceiver;

        [Header("Virtual Light")]
        [Tooltip("Directional Light 오브젝트를 연결하세요. 비워두면 씬의 Directional Light를 자동으로 찾습니다.")]
        public Light directionalLight;

        [Header("Shadow Settings")]
        [Range(0f, 1f)]  public float shadowStrength  = 0.7f;
        public int shadowTexSize = 64;
        [Range(1, 16)]   public int   sampleStride    = 8;
        [Range(0f, 2f)]  public float padding         = 0.3f;
        [Range(0, 8)]    public int   blurIterations  = 2;
        [Tooltip("각 프레임 그림자가 완전히 사라지는 데 걸리는 시간(초). " +
                 "이 시간 동안 과거 프레임 그림자들이 독립적으로 fade out되며 중첩됩니다.")]
        [Range(0.1f, 3f)] public float shadowFadeTime = 1.0f;
        [Tooltip("링버퍼 슬롯 수. shadowFadeTime * 예상FPS 정도로 설정. 30fps*1초=30, 30fps*2초=60.")]
        [Range(8, 60)]   public int   ringBufferSize  = 30;
        [Tooltip("그림자 계산 빈도. 1=매 프레임, 2=2프레임에 1번, 3=3프레임에 1번.")]
        [Range(1, 16)]   public int   updateInterval  = 1;

        // ── 내부 상태 ──────────────────────────────────────────────
        GaussianSplatPlayer m_HumanPlayer;
        bool                m_FrameChanged;
        GraphicsBuffer      m_WorldPosBuf;
        Texture2D           m_ShadowTex;
        int                 m_KernelExtract;
        bool                m_Ready;
        bool                m_ReadbackPending;

        // 링버퍼: 각 슬롯 = 해당 프레임의 그림자 R값(0=그림자, 1=없음) + 생성 시각
        float[][]           m_RingR;       // [슬롯][픽셀] R채널
        float[]             m_RingTime;    // [슬롯] 그림자가 생성된 Time.time
        float[]             m_RingG;       // G채널은 최신 readback 1개만 유지
        int                 m_RingHead;    // 다음에 쓸 슬롯 인덱스
        int                 m_RingCount;   // 현재 유효한 슬롯 수
        bool                m_RingReady;   // 링버퍼 초기화 완료 여부

        // 합산 결과 픽셀
        Color[]             m_FinalPixels;

        // 계산 빈도 제어
        int                 m_UpdateCounter;

        Vector3 m_LightRight;
        Vector3 m_LightUp;
        Vector3 m_LightDir;

        // 에디터 변화 감지
        Vector3             m_LastCasterPos;
        Quaternion          m_LastCasterRot;
        Vector3             m_LastCasterScale;
        GaussianSplatAsset  m_LastAsset;
        Quaternion          m_LastLightRot;

        // ── OnEnable ───────────────────────────────────────────────
        void OnEnable()
        {
            if (!ValidateReferences()) return;

            m_KernelExtract = shadowCaster.m_CSSplatUtilities.FindKernel("CSExtractWorldPos");
            if (m_KernelExtract < 0)
            {
                Debug.LogError("[GaussianShadowSystem] CSExtractWorldPos 커널을 찾지 못했습니다.");
                return;
            }

            m_ShadowTex = new Texture2D(shadowTexSize, shadowTexSize, TextureFormat.RGFloat, false);
            ClearShadowTex(1f);
            Shader.SetGlobalTexture("_ShadowTex",      m_ShadowTex);
            Shader.SetGlobalFloat  ("_ShadowStrength", shadowStrength);

            EnableShadowOnReceiver();

            m_HumanPlayer = shadowCaster.GetComponent<GaussianSplatPlayer>();
            if (m_HumanPlayer != null)
                m_HumanPlayer.onFrameChanged.AddListener(OnHumanFrameChanged);

            m_Ready = true;
            RequestUpdate();
        }

        // ── OnDisable ──────────────────────────────────────────────
        void OnDisable()
        {
            if (m_HumanPlayer != null)
                m_HumanPlayer.onFrameChanged.RemoveListener(OnHumanFrameChanged);

            m_WorldPosBuf?.Dispose();
            m_WorldPosBuf = null;
            m_RingReady     = false;
            m_UpdateCounter = 0;
            m_RingR         = null;
            m_RingTime    = null;
            m_RingG       = null;
            m_FinalPixels = null;
            m_Ready       = false;

            DisableShadowOnReceiver();
            ClearGlobalShadow();
        }

        // ── 링버퍼 초기화 ─────────────────────────────────────────
        void InitRingBuffer(int texLen)
        {
            m_RingR     = new float[ringBufferSize][];
            for (int s = 0; s < ringBufferSize; s++)
            {
                m_RingR[s] = new float[texLen];
                for (int i = 0; i < texLen; i++) m_RingR[s][i] = 1f;
            }
            m_RingTime    = new float[ringBufferSize];
            m_RingG       = new float[texLen];
            m_FinalPixels = new Color[texLen];
            for (int i = 0; i < texLen; i++) m_FinalPixels[i] = Color.white;
            m_RingHead  = 0;
            m_RingCount = 0;
            m_RingReady = true;
        }

        // ── LateUpdate ────────────────────────────────────────────
        void LateUpdate()
        {
            if (!m_Ready) return;
            if (!IsCasterReady()) return;

            if (Application.isPlaying)
            {
                // ── 플레이 중: 링버퍼 합산 → 텍스처 업로드 ──────
                if (m_RingReady && m_FinalPixels != null &&
                    m_FinalPixels.Length == m_ShadowTex.width * m_ShadowTex.height)
                {
                    int texLen = m_FinalPixels.Length;
                    float now  = Time.time;

                    for (int i = 0; i < texLen; i++)
                    {
                        float shadowSum = 0f;
                        float weightSum = 0f;
                        float gVal = m_RingG[i];

                        for (int s = 0; s < m_RingCount; s++)
                        {
                            int slot   = (m_RingHead - 1 - s + ringBufferSize) % ringBufferSize;
                            float age  = now - m_RingTime[slot];
                            if (age > shadowFadeTime) continue;

                            float half = Mathf.Max(shadowFadeTime * 0.5f, 0.001f);
                            float weight;
                            if (age < half)
                                weight = age / half;
                            else
                                weight = 1f - (age - half) / half;
                            weight = Mathf.Clamp01(weight);

                            float darkness = (1f - m_RingR[slot][i]) * weight;
                            shadowSum += darkness;
                            weightSum += weight;
                        }

                        float finalDarkness = weightSum > 0f
                            ? Mathf.Clamp01(shadowSum / weightSum)
                            : 0f;
                        m_FinalPixels[i] = new Color(1f - finalDarkness, gVal, 0f, 1f);
                    }

                    m_ShadowTex.SetPixels(m_FinalPixels);
                    m_ShadowTex.Apply();
                    Shader.SetGlobalTexture("_ShadowTex", m_ShadowTex);
                }

                // dispatch: updateInterval 프레임마다 1회
                if (!m_ReadbackPending)
                {
                    m_UpdateCounter++;
                    if (m_UpdateCounter >= updateInterval)
                    {
                        m_UpdateCounter = 0;
                        if (m_HumanPlayer == null) { DispatchAndReadback(); return; }
                        if (m_FrameChanged)
                        {
                            m_FrameChanged = false;
                            DispatchAndReadback();
                        }
                    }
                }
            }
            else
            {
                // ── 에디터 모드: fade/중첩 없이 최신 그림자 1개만 표시 ──
                // readback 완료 시 OnReadbackComplete에서 직접 텍스처 업로드.
                if (!m_ReadbackPending && HasChanged())
                    DispatchAndReadback();
            }
        }

        // ── 유효성 검사 ───────────────────────────────────────────
        bool ValidateReferences()
        {
            if (shadowCaster == null)
            {
                Debug.LogWarning("[GaussianShadowSystem] Shadow Caster가 연결되지 않았습니다.");
                return false;
            }
            if (shadowCaster.m_CSSplatUtilities == null)
            {
                Debug.LogWarning("[GaussianShadowSystem] Shadow Caster의 GaussianSplatRenderer에 CS Splat Utilities가 없습니다.");
                return false;
            }
            return true;
        }

        bool IsCasterReady() =>
            shadowCaster != null &&
            shadowCaster.HasValidAsset &&
            shadowCaster.HasValidRenderSetup;

        void EnableShadowOnReceiver()
        {
            var mat = GetReceiverMaterial();
            if (mat != null) mat.EnableKeyword("_SHADOW_ON");
        }

        void DisableShadowOnReceiver()
        {
            var mat = GetReceiverMaterial();
            if (mat != null) mat.DisableKeyword("_SHADOW_ON");
        }

        Material GetReceiverMaterial()
        {
            if (shadowReceiver == null) return null;
            shadowReceiver.EnsureMaterials();
            var f = typeof(GaussianSplatRenderer).GetField(
                "m_MatSplats",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f?.GetValue(shadowReceiver) as Material;
        }

        bool HasChanged()
        {
            if (shadowCaster == null) return false;
            var t        = shadowCaster.transform;
            var asset    = shadowCaster.asset;
            var lightRot = directionalLight != null
                ? directionalLight.transform.rotation : Quaternion.identity;

            bool changed =
                t.position   != m_LastCasterPos   ||
                t.rotation   != m_LastCasterRot   ||
                t.localScale != m_LastCasterScale ||
                asset        != m_LastAsset       ||
                lightRot     != m_LastLightRot;

            if (changed)
            {
                m_LastCasterPos   = t.position;
                m_LastCasterRot   = t.rotation;
                m_LastCasterScale = t.localScale;
                m_LastAsset       = asset;
                m_LastLightRot    = lightRot;
            }
            return changed;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!m_Ready) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                Shader.SetGlobalFloat("_ShadowStrength", shadowStrength);
                RequestUpdate();
            };
        }
#endif

        void OnHumanFrameChanged(int _) => m_FrameChanged = true;

        void RequestUpdate()
        {
            if (!m_Ready || !IsCasterReady()) return;
            DispatchAndReadback();
        }

        // ── GPU Dispatch + Async Readback ─────────────────────────
        void DispatchAndReadback()
        {
            int totalSplats = shadowCaster.splatCount;
            int sampleCount = Mathf.Max(1, totalSplats / sampleStride);

            if (m_WorldPosBuf == null || m_WorldPosBuf.count != sampleCount)
            {
                m_WorldPosBuf?.Dispose();
                m_WorldPosBuf = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured, sampleCount, sizeof(float) * 3)
                    { name = "GaussianShadow_WorldPos" };
            }

            var  asset = shadowCaster.asset;
            uint fmt   = (uint)asset.posFormat | ((uint)asset.scaleFormat << 8) | ((uint)asset.shFormat << 16);

            var cs = shadowCaster.m_CSSplatUtilities;
            cs.SetBuffer(m_KernelExtract, "_SplatPos",    GetBuffer(shadowCaster, "m_GpuPosData"));
            cs.SetBuffer(m_KernelExtract, "_SplatChunks", GetBuffer(shadowCaster, "m_GpuChunks"));
            cs.SetInt   ("_SplatCount",      totalSplats);
            cs.SetInt   ("_SplatChunkCount", GetChunkCount(shadowCaster));
            cs.SetInt   ("_SplatFormat",     (int)fmt);
            cs.SetMatrix("_MatrixObjectToWorld", shadowCaster.transform.localToWorldMatrix);
            cs.SetInt   ("_WorldPosStride",  sampleStride);
            cs.SetInt   ("_WorldPosCount",   sampleCount);
            cs.SetBuffer(m_KernelExtract, "_SplatWorldPosBuf", m_WorldPosBuf);

            cs.GetKernelThreadGroupSizes(m_KernelExtract, out uint gsX, out _, out _);
            cs.Dispatch(m_KernelExtract, Mathf.CeilToInt((float)sampleCount / gsX), 1, 1);

            Shader.SetGlobalFloat("_ShadowStrength", shadowStrength);

            if (directionalLight == null)
                directionalLight = FindFirstObjectByType<Light>();

            m_LightDir = directionalLight != null
                ? directionalLight.transform.forward
                : new Vector3(-1f, -2f, -0.5f).normalized;

            m_LightRight = Vector3.Cross(Vector3.up, m_LightDir).normalized;
            if (m_LightRight.magnitude < 0.001f)
                m_LightRight = Vector3.Cross(Vector3.forward, m_LightDir).normalized;
            m_LightUp = Vector3.Cross(m_LightDir, m_LightRight).normalized;

            m_ReadbackPending = true;
            AsyncGPUReadback.Request(m_WorldPosBuf, OnReadbackComplete);
        }

        // ── Readback 완료 → 링버퍼에 새 슬롯 추가 ───────────────
        void OnReadbackComplete(AsyncGPUReadbackRequest req)
        {
            m_ReadbackPending = false;
            if (req.hasError) { Debug.LogError("[GaussianShadowSystem] GPU Readback 실패"); return; }
            if (m_ShadowTex == null) return;

            int texLen = shadowTexSize * shadowTexSize;

            // 텍스처 크기 변경 또는 링버퍼 미초기화 시 재생성
            if (m_ShadowTex.width != shadowTexSize || m_ShadowTex.height != shadowTexSize)
            {
                DestroyImmediate(m_ShadowTex);
                m_ShadowTex = new Texture2D(shadowTexSize, shadowTexSize, TextureFormat.RGFloat, false);
                m_RingReady = false;
            }

            if (!m_RingReady || m_RingR == null || m_RingR.Length != ringBufferSize ||
                m_RingR[0].Length != texLen)
                InitRingBuffer(texLen);

            var positions = req.GetData<Vector3>();
            int count     = positions.Length;

            // Light 공간 UV 범위
            float minU = float.MaxValue, maxU = float.MinValue;
            float minV = float.MaxValue, maxV = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                float u = Vector3.Dot(positions[i], m_LightRight);
                float v = Vector3.Dot(positions[i], m_LightUp);
                if (u < minU) minU = u; if (u > maxU) maxU = u;
                if (v < minV) minV = v; if (v > maxV) maxV = v;
            }

            float rangeU = maxU - minU, rangeV = maxV - minV;
            float padU   = rangeU * padding, padV = rangeV * padding;
            minU -= padU; maxU += padU; minV -= padV; maxV += padV;
            rangeU = maxU - minU; rangeV = maxV - minV;
            if (rangeU < 0.001f || rangeV < 0.001f) return;

            // 새 슬롯에 이번 프레임 그림자 기록
            int slot = m_RingHead;
            m_RingTime[slot] = Time.time;
            float[] slotR = m_RingR[slot];
            for (int i = 0; i < texLen; i++) slotR[i] = 1f;

            float minDepth = float.MaxValue, maxDepth = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                float d = Vector3.Dot(positions[i], m_LightDir);
                if (d < minDepth) minDepth = d; if (d > maxDepth) maxDepth = d;
            }
            float depthRange = Mathf.Max(maxDepth - minDepth, 0.001f);

            int dotSize = Mathf.Max(1, shadowTexSize / 64);
            for (int i = 0; i < count; i++)
            {
                float u     = Vector3.Dot(positions[i], m_LightRight);
                float v     = Vector3.Dot(positions[i], m_LightUp);
                float depth = Vector3.Dot(positions[i], m_LightDir);
                float normD = (depth - minDepth) / depthRange;

                int px = Mathf.RoundToInt((u - minU) / rangeU * (shadowTexSize - 1));
                int py = Mathf.RoundToInt((v - minV) / rangeV * (shadowTexSize - 1));

                for (int dy = -dotSize; dy <= dotSize; dy++)
                for (int dx = -dotSize; dx <= dotSize; dx++)
                {
                    int x = px + dx, y = py + dy;
                    if (x < 0 || x >= shadowTexSize || y < 0 || y >= shadowTexSize) continue;
                    int idx = y * shadowTexSize + x;
                    if (slotR[idx] > 0.5f || normD < m_RingG[idx])
                    {
                        slotR[idx]   = 0f;
                        m_RingG[idx] = normD;
                    }
                }
            }

            // Blur는 슬롯 R에 적용
            float[] blurred = BoxBlurR(slotR, shadowTexSize);
            System.Array.Copy(blurred, slotR, texLen);

            // 에디터 모드: 링버퍼 합산 없이 이 슬롯을 바로 텍스처에 업로드
            if (!Application.isPlaying)
            {
                if (m_FinalPixels == null || m_FinalPixels.Length != texLen)
                    m_FinalPixels = new Color[texLen];
                for (int i = 0; i < texLen; i++)
                    m_FinalPixels[i] = new Color(slotR[i], m_RingG[i], 0f, 1f);
                m_ShadowTex.SetPixels(m_FinalPixels);
                m_ShadowTex.Apply();
                Shader.SetGlobalTexture("_ShadowTex", m_ShadowTex);
            }

            // 링버퍼 포인터 전진
            m_RingHead = (m_RingHead + 1) % ringBufferSize;
            if (m_RingCount < ringBufferSize) m_RingCount++;

            // BG 셰이더에 Light 공간 행렬 전달
            Matrix4x4 worldToLightUV = Matrix4x4.identity;
            worldToLightUV.SetRow(0, new Vector4(m_LightRight.x / rangeU, m_LightRight.y / rangeU, m_LightRight.z / rangeU, -minU / rangeU));
            worldToLightUV.SetRow(1, new Vector4(m_LightUp.x    / rangeV, m_LightUp.y    / rangeV, m_LightUp.z    / rangeV, -minV / rangeV));
            worldToLightUV.SetRow(2, new Vector4(0, 0, 0, 0));
            worldToLightUV.SetRow(3, new Vector4(0, 0, 0, 1));

            Shader.SetGlobalMatrix("_WorldToLightMatrix", worldToLightUV);
            Shader.SetGlobalMatrix("_ShadowObjToWorld",
                shadowReceiver != null ? shadowReceiver.transform.localToWorldMatrix : Matrix4x4.identity);
            Shader.SetGlobalFloat ("_LightDepthMin",   minDepth);
            Shader.SetGlobalFloat ("_LightDepthRange", depthRange);
            Shader.SetGlobalVector("_ShadowLightDir",  m_LightDir);

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        // ── 유틸 ─────────────────────────────────────────────────
        void ClearShadowTex(float value)
        {
            if (m_ShadowTex == null) return;
            var pixels = new Color[shadowTexSize * shadowTexSize];
            var col    = new Color(value, value, 0f, 1f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
            m_ShadowTex.SetPixels(pixels);
            m_ShadowTex.Apply();
        }

        static readonly float[] k_GaussianKernel =
        {
            1,  4,  6,  4, 1,
            4, 16, 24, 16, 4,
            6, 24, 36, 24, 6,
            4, 16, 24, 16, 4,
            1,  4,  6,  4, 1,
        };

        // R채널 전용 Blur (Color[] 대신 float[] 처리로 약간 가벼움)
        float[] BoxBlurR(float[] src, int size)
        {
            var dst = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float sum = 0, totalW = 0;
                for (int ky = 0; ky < 5; ky++)
                for (int kx = 0; kx < 5; kx++)
                {
                    int nx = x + kx - 2, ny = y + ky - 2;
                    if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                    float w = k_GaussianKernel[ky * 5 + kx];
                    sum    += src[ny * size + nx] * w;
                    totalW += w;
                }
                dst[y * size + x] = sum / totalW;
            }
            return dst;
        }

        void ClearGlobalShadow()
        {
            var tex    = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            Shader.SetGlobalTexture("_ShadowTex",      tex);
            Shader.SetGlobalFloat  ("_ShadowStrength", 0f);
        }

        GraphicsBuffer GetBuffer(GaussianSplatRenderer r, string fieldName)
        {
            var f = typeof(GaussianSplatRenderer).GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f?.GetValue(r) as GraphicsBuffer;
        }

        int GetChunkCount(GaussianSplatRenderer r)
        {
            var f = typeof(GaussianSplatRenderer).GetField(
                "m_GpuChunksValid",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool valid = f != null && (bool)f.GetValue(r);
            return valid ? (GetBuffer(r, "m_GpuChunks")?.count ?? 0) : 0;
        }
    }
}
