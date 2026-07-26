using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace EvacuationShelter.Voice
{
    public enum EmotionType
    {
        Normal,
        Happy,
        Sad,
        Angry,
        Surprised
    }

    /// <summary>
    /// キャラクターの「演技（音声＋表情＋口パク）」を制御するコンポーネント
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CharacterActingController : MonoBehaviour
    {
        [Header("コンポーネント設定")]
        [SerializeField] private VoicevoxClient voicevoxClient;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private SkinnedMeshRenderer faceMeshRenderer;
        [SerializeField] private Animator characterAnimator;

        [Header("BlendShape (表情) 設定")]
        [Tooltip("口を開けるBlendShapeの名前 (例: Fcl_MTH_A, mouth_open, Mouth_A)")]
        [SerializeField] private string mouthOpenBlendShapeName = "Fcl_MTH_A";
        
        [SerializeField] private string happyBlendShapeName = "Fcl_ALL_Joy";
        [SerializeField] private string sadBlendShapeName = "Fcl_ALL_Sorrow";
        [SerializeField] private string angryBlendShapeName = "Fcl_ALL_Angry";
        [SerializeField] private string surprisedBlendShapeName = "Fcl_ALL_Surprised";

        [Header("演技パラメータ")]
        [SerializeField] private float blendShapeTransitionSpeed = 5.0f;
        [SerializeField] private float mouthSensitivity = 150.0f;
        
        [Tooltip("音量倍率 (1.0 = 標準, 2.0 = 2倍の音量)")]
        [SerializeField] [Range(0.1f, 5.0f)] private float volumeScale = 2.0f;

        private int mouthOpenIndex = -1;
        private int happyIndex = -1;
        private int sadIndex = -1;
        private int angryIndex = -1;
        private int surprisedIndex = -1;

        private float targetMouthWeight = 0f;
        private float currentMouthWeight = 0f;
        private EmotionType currentEmotion = EmotionType.Normal;

        private float[] audioSamples = new float[256];

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (voicevoxClient == null) voicevoxClient = GetComponent<VoicevoxClient>();
            if (voicevoxClient == null) voicevoxClient = FindObjectOfType<VoicevoxClient>();

            if (audioSource != null)
            {
                audioSource.volume = 1.0f;
                // 2Dサウンド（距離で減衰しない聞き取りやすい設定）
                audioSource.spatialBlend = 0.0f;
            }

            CacheBlendShapeIndices();
        }

        /// <summary>
        /// BlendShapeの名前からインデックスをキャッシュします
        /// </summary>
        public void CacheBlendShapeIndices()
        {
            if (faceMeshRenderer == null || faceMeshRenderer.sharedMesh == null) return;

            Mesh mesh = faceMeshRenderer.sharedMesh;
            mouthOpenIndex = mesh.GetBlendShapeIndex(mouthOpenBlendShapeName);
            happyIndex = mesh.GetBlendShapeIndex(happyBlendShapeName);
            sadIndex = mesh.GetBlendShapeIndex(sadBlendShapeName);
            angryIndex = mesh.GetBlendShapeIndex(angryBlendShapeName);
            surprisedIndex = mesh.GetBlendShapeIndex(surprisedBlendShapeName);
        }

        private void Update()
        {
            UpdateLipSync();
            UpdateFacialExpressions();
        }

        /// <summary>
        /// テキストを受け取り、音声生成＋表情＋リップシンク演技を実行します。
        /// </summary>
        public async Task PerformActingAsync(string text, EmotionType emotion, int? speakerId = null)
        {
            SetEmotion(emotion);

            if (voicevoxClient == null)
            {
                voicevoxClient = GetComponent<VoicevoxClient>();
                if (voicevoxClient == null)
                {
                    voicevoxClient = FindObjectOfType<VoicevoxClient>();
                }
                if (voicevoxClient == null)
                {
                    Debug.Log("[CharacterActingController] VoicevoxClient を自動アタッチします。");
                    voicevoxClient = gameObject.AddComponent<VoicevoxClient>();
                }
            }

            // 音声生成
            AudioClip clip = await voicevoxClient.GenerateVoiceAsync(text, speakerId, volumeScale: volumeScale);
            if (clip != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
            }
        }

        public void SetEmotion(EmotionType emotion)
        {
            currentEmotion = emotion;
            Debug.Log($"[CharacterActingController] 感情変更: {emotion}");
        }

        private void UpdateLipSync()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.GetOutputData(audioSamples, 0);
                float sum = 0f;
                for (int i = 0; i < audioSamples.Length; i++)
                {
                    sum += audioSamples[i] * audioSamples[i];
                }
                float rms = Mathf.Sqrt(sum / audioSamples.Length);
                targetMouthWeight = Mathf.Clamp(rms * mouthSensitivity, 0f, 100f);
            }
            else
            {
                targetMouthWeight = 0f;
            }

            currentMouthWeight = Mathf.Lerp(currentMouthWeight, targetMouthWeight, Time.deltaTime * blendShapeTransitionSpeed * 2);

            // 3Dモデルの BlendShape がある場合
            if (faceMeshRenderer != null && mouthOpenIndex >= 0)
            {
                faceMeshRenderer.SetBlendShapeWeight(mouthOpenIndex, currentMouthWeight);
            }
            // 仮オブジェクト（Capsule等）の場合の視覚フィードバック（上下の拡大縮小で口パクを表現）
            else if (faceMeshRenderer == null)
            {
                float mouthScaleOffset = (currentMouthWeight / 100f) * 0.2f;
                transform.localScale = new Vector3(1f, 1f + mouthScaleOffset, 1f);
            }
        }

        private void UpdateFacialExpressions()
        {
            if (faceMeshRenderer == null) return;

            float speed = Time.deltaTime * blendShapeTransitionSpeed;

            SetBlendShapeTarget(happyIndex, currentEmotion == EmotionType.Happy ? 100f : 0f, speed);
            SetBlendShapeTarget(sadIndex, currentEmotion == EmotionType.Sad ? 100f : 0f, speed);
            SetBlendShapeTarget(angryIndex, currentEmotion == EmotionType.Angry ? 100f : 0f, speed);
            SetBlendShapeTarget(surprisedIndex, currentEmotion == EmotionType.Surprised ? 100f : 0f, speed);
        }

        private void SetBlendShapeTarget(int index, float targetWeight, float speed)
        {
            if (index < 0) return;
            float current = faceMeshRenderer.GetBlendShapeWeight(index);
            float updated = Mathf.Lerp(current, targetWeight, speed);
            faceMeshRenderer.SetBlendShapeWeight(index, updated);
        }
    }
}
