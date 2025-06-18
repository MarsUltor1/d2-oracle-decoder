using System;
using System.Numerics;
using System.Collections.Generic;
using NAudio.Wave;
using FftSharp;
using NAudio.CoreAudioApi;

namespace d2OracleDecoder 
{
    enum Oracle
    {
        L1,
        L2,
        L3,
        M,
        R1,
        R2,
        R3,
        NaO
    }

    class Program
    {
        private static int sampleRate = 0;
        private static int fftSize = 4096;

        static void Main(string[] args)
        {
            // Repeat for as many sequences as user wants
            while (true)
            {
                // Record the oracles sequence
                RecordAudio();

                // Analyze recording to figure out pattern
                List<Oracle> oracles = AnalyzeAudio();

                // Output oracle sequence to user
                Console.WriteLine("\nSolution: ");
                foreach (Oracle o in oracles)
                {
                    Console.WriteLine(o);
                }
            }
        }

        private static void RecordAudio ()
        {
            // only record when delete key is hit
            Console.WriteLine("\n\nPress [del] (delete) to start recording.");
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

        private static List<Oracle> AnalyzeAudio ()
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
                List<Oracle> oracles = new List<Oracle>();
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

                    // Convert peak frequency an oracle
                    Oracle currentOracle = CheckForOracle(peakFrequency);
                    if ( currentOracle != Oracle.NaO)
                    {
                        // check that the oracle has not already been added to list
                        if (!oracles.Contains(currentOracle))
                        {
                            oracles.Add(currentOracle);
                        }
                    }
                }

                return oracles;
            }
        }

        private static Oracle CheckForOracle(double frequency)
        {
            // Match frequency with corosponding oracle
            switch (frequency)
            {
                case 292.96875: // L1
                    return Oracle.L1;

                case 363.28125: // L2
                    return Oracle.L2;

                case 785.15625: // L3
                    return Oracle.L3;

                case 257.8125: // M
                    return Oracle.M;

                case 667.96875: // R1
                    return Oracle.R1;

                case 105.46875: // R2
                    return Oracle.R2;

                case 480.46875: // R3
                    return Oracle.R3;

                default: // If no matching frequency is found return Not an Oracle
                    return Oracle.NaO;
            }
            
        }
    }
}