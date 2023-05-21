using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using CSCore;
using CSCore.SoundIn;
using CSCore.Streams;


namespace LightstripSyncClient
{
    public class ColorSync
    {
        private bool loop = false;

        private readonly double smoothSpeed = 0.8;
        private readonly int refreshRate = 2;

        public void ToggleSync(bool state, BluetoothLEConnectionManager bluetoothLEConnectionManager)
        {
            loop = state;

            if (loop)
            {
                // Create a separate thread for audio processing
                Thread audioThread = new Thread(() =>
                {
                    // Create an instance of the sound capture device
                    using (var captureDevice = new WasapiLoopbackCapture())
                    {
                        // Set up event handlers for audio data capture
                        captureDevice.Initialize();

                        // Create a sound source to read audio data from the capture device
                        using (var soundSource = new SoundInSource(captureDevice))
                        {
                            // Create a sample source to provide audio data to the sound source
                            using (var sampleSource = soundSource.ToSampleSource())
                            {
                                // Specify the desired sample rate and number of channels for audio processing
                                const int desiredSampleRate = 44100;
                                const int desiredChannels = 2;

                                // Resample and convert the audio if necessary
                                var resampledSource = sampleSource;

                                if (sampleSource.WaveFormat.SampleRate != desiredSampleRate || sampleSource.WaveFormat.Channels != desiredChannels)
                                {
                                    resampledSource = sampleSource.ChangeSampleRate(desiredSampleRate).ToStereo();
                                }

                                float[] buffer = new float[desiredSampleRate / 10 * desiredChannels];

                                // Start capturing audio data
                                captureDevice.Start();

                                var oldColor = Color.White;

                                // Audio processing loop
                                while (loop)
                                {
                                    // Read audio samples into the buffer
                                    int samplesRead = resampledSource.Read(buffer, 0, buffer.Length);

                                    // Check if any samples were read
                                    if (samplesRead > 0)
                                    {
                                        // Process the audio samples to determine the LED color
                                        var newColor = ProcessAudioSamples(buffer, samplesRead);

                                        // Smooth the color transition
                                        newColor = SmoothColor(oldColor, newColor, smoothSpeed);

                                        // Change the LED color using the BluetoothLEConnectionManager
                                        bluetoothLEConnectionManager.ChangeColor(newColor);

                                        // Update the oldColor variable
                                        oldColor = newColor;
                                    }

                                    // Delay for a short period to control the refresh rate
                                    System.Threading.Thread.Sleep(refreshRate);
                                }

                                // Stop capturing audio data
                                captureDevice.Stop();
                            }
                        }
                    }
                });

                // Start the audio processing thread
                audioThread.Start();
            }
        }



        private Color ProcessAudioSamples(float[] buffer, int samplesRead)
        {
            if (samplesRead > 0)
            {
                // Perform audio analysis on the buffer and extract relevant information

                // Calculate the average amplitude of the audio samples
                float averageAmplitude = buffer.Take(samplesRead).Average(Math.Abs);

                // Calculate the desired RGB values based on the audio analysis
                int red = (int)(averageAmplitude * 255);
                int green = 0;
                int blue = 0;

                // Clamp the RGB values within the valid range (0-255)
                red = Math.Max(0, Math.Min(255, red));
                green = Math.Max(0, Math.Min(255, green));
                blue = Math.Max(0, Math.Min(255, blue));

                // Create and return the new color based on the RGB values
                return Color.FromArgb(red, green, blue);
            }

            // If no samples were read, return a default color (e.g., black)
            return Color.Black;
        }

        private Color SmoothColor(Color oldCol, Color newCol, double time)
        {
            var vector = new Vector3(newCol.R - oldCol.R, newCol.G - oldCol.G, newCol.B - oldCol.B);
            var adjustedVector = new Vector3(vector.x * time, vector.y * time, vector.z * time);

            var SmoothedColorVector = new Vector3(oldCol.R + adjustedVector.x, oldCol.G + adjustedVector.y, oldCol.B + adjustedVector.z);

            var SmoothedColor = Color.FromArgb((int)SmoothedColorVector.x, (int)SmoothedColorVector.y, (int)SmoothedColorVector.z);
            return SmoothedColor;
        }

        private struct Vector3
        {
            public double x, y, z;
            public Vector3(double x, double y, double z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }
    }
}

