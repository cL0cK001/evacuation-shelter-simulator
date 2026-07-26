using System;
using UnityEngine;

namespace EvacuationShelter.Voice
{
    /// <summary>
    /// WAVバイナリ（byte[]）をUnityのAudioClipに変換するユーティリティクラス
    /// </summary>
    public static class WavUtility
    {
        public static AudioClip ToAudioClip(byte[] wavBytes, string clipName = "VoicevoxClip")
        {
            if (wavBytes == null || wavBytes.Length < 44)
            {
                Debug.LogError("[WavUtility] WAVデータが無効または短すぎます。");
                return null;
            }

            try
            {
                // WAVヘッダー解析
                int channels = BitConverter.ToInt16(wavBytes, 22);
                int sampleRate = BitConverter.ToInt32(wavBytes, 24);
                ushort bitDepth = BitConverter.ToUInt16(wavBytes, 34);

                // 'data' サブチャンクの位置を探す
                int pos = 12;
                while (pos < wavBytes.Length - 8)
                {
                    string chunkId = System.Text.Encoding.ASCII.GetString(wavBytes, pos, 4);
                    int chunkSize = BitConverter.ToInt32(wavBytes, pos + 4);

                    if (chunkId == "data")
                    {
                        pos += 8;
                        int pcmDataSize = chunkSize;
                        if (pos + pcmDataSize > wavBytes.Length)
                        {
                            pcmDataSize = wavBytes.Length - pos;
                        }

                        float[] samples;
                        if (bitDepth == 16)
                        {
                            int sampleCount = pcmDataSize / 2;
                            samples = new float[sampleCount];
                            for (int i = 0; i < sampleCount; i++)
                            {
                                short pcm = BitConverter.ToInt16(wavBytes, pos + i * 2);
                                samples[i] = pcm / 32768.0f;
                            }
                        }
                        else if (bitDepth == 8)
                        {
                            int sampleCount = pcmDataSize;
                            samples = new float[sampleCount];
                            for (int i = 0; i < sampleCount; i++)
                            {
                                byte pcm = wavBytes[pos + i];
                                samples[i] = (pcm - 128) / 128.0f;
                            }
                        }
                        else if (bitDepth == 32)
                        {
                            int sampleCount = pcmDataSize / 4;
                            samples = new float[sampleCount];
                            for (int i = 0; i < sampleCount; i++)
                            {
                                samples[i] = BitConverter.ToSingle(wavBytes, pos + i * 4);
                            }
                        }
                        else
                        {
                            Debug.LogError($"[WavUtility] 未対応のビット深度: {bitDepth}");
                            return null;
                        }

                        AudioClip audioClip = AudioClip.Create(clipName, samples.Length / channels, channels, sampleRate, false);
                        audioClip.SetData(samples, 0);
                        return audioClip;
                    }

                    pos += 8 + chunkSize;
                }

                Debug.LogError("[WavUtility] WAVデータ内に 'data' チャンクが見つかりませんでした。");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WavUtility] WAV変換中にエラーが発生しました: {ex.Message}");
                return null;
            }
        }
    }
}
