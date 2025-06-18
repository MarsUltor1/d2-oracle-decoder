using System;
using System.Numerics;
using System.Collections.Generic;
using NAudio.Wave;
using FftSharp;
using NAudio.CoreAudioApi;

namespace d2OracleDecoder 
{
    class Program
    {
        private static int sampleRate = 0;
        private static int fftSize = 4096;

        static void Main(string[] args)
        {
            // Record the oracles sequence
            RecordAudio();

            // Analyze recording to figure out pattern
            Thread.Sleep(2000); // pause between recording and playback for testing purposes only
            AnalyzeAudio();
        }

        private static void RecordAudio ()
        {
            // only record when delete key is hit
            Console.WriteLine("Press [del] (delete) to start recording.");
            while (true)
            {
                if (Console.ReadKey().Key == ConsoleKey.Delete)
                {
                    break;
                }
            }

            // store each recording in the same file, overwriting the old recordings
            using (WasapiLoopbackCapture capture = new WasapiLoopbackCapture())
            {
                sampleRate = capture.WaveFormat.SampleRate;
                using (WaveFileWriter waveFile = new WaveFileWriter("oracles.wav", capture.WaveFormat))
                {
                    // Setup capture recording function
                    capture.DataAvailable += async (s, e) =>
                    {
                        if (waveFile != null)
                        {
                            await waveFile.WriteAsync(e.Buffer, 0, e.BytesRecorded);
                            await waveFile.FlushAsync();
                        }
                    };

                    // Start recording
                    capture.StartRecording();

                    // Check for stop recording input
                    Console.WriteLine("Recording Oracles... Press [del] (delete) to stop recording.");
                    while (true)
                    {
                        if (Console.ReadKey().Key == ConsoleKey.Delete)
                        {
                            break;
                        }
                    }

                    // Stop the recording
                    capture.StopRecording();
                    Console.WriteLine("Recording Stopped.");
                }
            }
        }

        private static void AnalyzeAudio ()
        {
            // Open audio file
            using (AudioFileReader waveFile = new AudioFileReader("oracles.wav"))
            {
                // Setup buffer based off file and fft variables
                int bytesPerSample = waveFile.WaveFormat.BitsPerSample / 8;
                int bufferSize = bytesPerSample * fftSize;
                byte[] buffer = new byte[bufferSize];

                // read in audio buffer to analyze
                int read;
                while ((read = waveFile.Read(buffer, 0, bufferSize)) > 0)
                {
                    // Convert bytes to float samples
                    float[] audioSamples = new float[read / bytesPerSample];
                    for (int i = 0; i < audioSamples.Length; i++)
                    {
                        audioSamples[i] = BitConverter.ToSingle(buffer, i * bytesPerSample);
                    }

                    // Add padding to avoid FFT.Forward error
                    double[] doubleSamples = new double[fftSize];
                    Array.Copy(audioSamples, doubleSamples, audioSamples.Length);

                    // Run fft
                    Complex[] fftResult = FFT.Forward(doubleSamples);
                    double[] magnitudes = FFT.Magnitude(fftResult);

                    // Identify peak frequencies
                    int peakIndex = Array.IndexOf(magnitudes, magnitudes.Max());
                    double peakFrequency = FFT.FrequencyScale(magnitudes.Length, sampleRate)[peakIndex];

                    // Convert peak frequency to note
                    Console.WriteLine($"peak frequency {peakFrequency}");
                }
            }
        }
    }
}