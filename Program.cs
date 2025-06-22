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
        private static List<Oracle> oracles = new List<Oracle>();

        static void Main(string[] args)
        {
            // Repeat for as many sequences as user wants
            while (true)
            {
                // Record the oracles sequence
                RecordAudio();
                
                // Analyze recording to figure out pattern
                oracles.Clear();
                AnalyzeAudio();

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
                    Console.WriteLine(peakFrequency);

                    // Convert peak frequency an oracle
                    // Hold possible oracle and it's frequency until it is confirmed
                    Oracle possibleOracle = Oracle.NaO;
                    double matchedFrequency = double.MinValue;

                    // First check to match the oracle
                    if (possibleOracle == Oracle.NaO)
                    {
                        possibleOracle = CheckForOracle(peakFrequency, possibleOracle, matchedFrequency);
                        if (possibleOracle != Oracle.NaO) matchedFrequency = peakFrequency;
                    }
                    else // check subsiquint peak frequencies for matches
                    {
                        // Check to see if two frequencies in a row match one oracle
                        Oracle oracle = CheckForOracle(peakFrequency, possibleOracle, matchedFrequency);

                        // check that a valid not already added oracle was found
                        if (oracle != Oracle.NaO && !oracles.Contains(oracle))
                        {
                            oracles.Add(oracle);
                        }

                        // reset possibleOracle and matched frequency so other oracles can be checked for
                        possibleOracle = Oracle.NaO;
                        matchedFrequency = double.MinValue;
                    }
                }
            }
        }

        private static Oracle CheckForOracle(double frequency, Oracle possibleOracle, double matchedFrequency)
        {
            List<double> l1Frequencies = new List<double>() { 23707.03125, 152.34375, 140.625, 304.6875, 292.96875 };
            List<double> l2Frequencies = new List<double>() { 714.84375, 23296.875, 363.28125 };
            List<double> l3Frequencies = new List<double>() { 773.4375, 23214.84375, 23542.96875, 445.3125, 433.59375, 785.15625 };
            List<double> mFrequencies = new List<double>() { 550.78125, 269.53125, 257.8125 };
            List<double> r1Frequencies = new List<double>() { 503.90625, 164.0625, 667.96875 };
            List<double> r2Frequencies = new List<double>() { 609.375, 23367.1875, 23894.53125, 398.4375, 632.8125, 105.46875};
            List<double> r3Frequencies = new List<double>() { 246.09375, 421.875, 23519.53125, 468.75, 480.46875 };

            // if no possible oracle has been identified look for all possible matches
            if (possibleOracle == Oracle.NaO)
            {
                // check if it matches any of the unique oracle frequencies
                if (l1Frequencies.Contains(frequency)) return Oracle.L1;
                else if (l2Frequencies.Contains(frequency)) return Oracle.L2;
                else if (l3Frequencies.Contains(frequency)) return Oracle.L3;
                else if (mFrequencies.Contains(frequency)) return Oracle.M;
                else if (r1Frequencies.Contains(frequency)) return Oracle.R1;
                else if (r2Frequencies.Contains(frequency)) return Oracle.R2;
                else if (r3Frequencies.Contains(frequency)) return Oracle.R3;
            }
            // If a possible oracle was found double check if another matching frequency was subsiquintly found
            else
            {
                // Check that we aren't matching the same frequency twic in a row
                if (frequency == matchedFrequency) return possibleOracle;

                switch (possibleOracle)
                { 
                    case Oracle.L1:
                        if (l1Frequencies.Contains(frequency)) return possibleOracle;
                        else return Oracle.NaO;

                    case Oracle.L2:
                        if (l1Frequencies.Contains(frequency)) return possibleOracle;
                        else return Oracle.NaO;

                    case Oracle.L3:
                        if (l1Frequencies.Contains(frequency)) return possibleOracle;
                        else return Oracle.NaO;

                    case Oracle.M:
                        if (l1Frequencies.Contains(frequency)) return possibleOracle;
                        else return Oracle.NaO;

                    case Oracle.R1:
                        if (l1Frequencies.Contains(frequency)) return possibleOracle;
                        else return Oracle.NaO;

                    case Oracle.R2:
                        if (l1Frequencies.Contains(frequency)) return possibleOracle;
                        else return Oracle.NaO;

                    case Oracle.R3:
                        if (l1Frequencies.Contains(frequency)) return possibleOracle;
                        else return Oracle.NaO;
                }
                    
            }

            // if all else fails return null
            return Oracle.NaO;
        }
    }
}