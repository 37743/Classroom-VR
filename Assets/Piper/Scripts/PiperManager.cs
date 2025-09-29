/// <summary>
/// PiperManager.cs
/// Manager component for Piper TTS
/// Specifically modified for Classroom VR from original source: https://github.com/Macoron/piper.unity/tree/master
/// Following the license: GPL v3
/// </summary>

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using UnityEngine;

namespace Piper
{
    public class PiperManager : MonoBehaviour
    {
        public Unity.InferenceEngine.ModelAsset model;
        public Unity.InferenceEngine.BackendType backend = Unity.InferenceEngine.BackendType.GPUCompute;
        public string voice = "en-GB-x-rp";
        public int sampleRate = 22050;

        // model asset
        private Unity.InferenceEngine.Model _runtimeModel;
        private Unity.InferenceEngine.Worker _worker;

        private void Awake()
        {
            var espeakPath = Path.Combine(Application.streamingAssetsPath, "espeak-ng-data");
            PiperWrapper.InitPiper(espeakPath);
            _runtimeModel = Unity.InferenceEngine.ModelLoader.Load(model);
            _worker = new Unity.InferenceEngine.Worker(_runtimeModel, backend);
        }

        public async Task<AudioClip> TextToSpeech(string text)
        {
            var phonemes = PiperWrapper.ProcessText(text, voice);

            var audioBuffer = new List<float>(32768);

            using var scalesTensor = new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(3), new float[] { 1f, 1f, .8f });

            for (int i = 0; i < phonemes.Sentences.Length; i++)
            {
                var sentence = phonemes.Sentences[i];
                var inputPhonemes = sentence.PhonemesIds;

                using var inputTensor = new Unity.InferenceEngine.Tensor<int>(new Unity.InferenceEngine.TensorShape(1, inputPhonemes.Length), inputPhonemes);
                using var inputLengthsTensor = new Unity.InferenceEngine.Tensor<int>(new Unity.InferenceEngine.TensorShape(1), new int[] { inputPhonemes.Length });

                _worker.SetInput("input", inputTensor);
                _worker.SetInput("input_lengths", inputLengthsTensor);
                _worker.SetInput("scales", scalesTensor);

                _worker.Schedule();

                var outputTensor = _worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>;
                if (outputTensor == null)
                {
                    Debug.LogError("Piper Sentis: output tensor is null or not Tensor<float>.");
                    continue;
                }

                using var cpuCopy = await outputTensor.ReadbackAndCloneAsync(); // async GPU->CPU
                var chunk = cpuCopy.DownloadToArray();
                audioBuffer.AddRange(chunk);
            }

            var data = audioBuffer.ToArray();
            var clip = AudioClip.Create("piper_tts", data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);

            return clip;
        }

        private void OnDestroy()
        {
            try { PiperWrapper.FreePiper(); } catch { }
            _worker?.Dispose();
        }
    }
}
