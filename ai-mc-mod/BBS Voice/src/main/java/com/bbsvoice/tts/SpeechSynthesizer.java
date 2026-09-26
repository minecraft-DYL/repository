package com.bbsvoice.tts;

import java.util.ArrayList;
import java.util.List;
import java.util.Random;

/**
 * Offline speech synthesiser. A classic source-filter (Klatt-ish) model:
 *
 * <ol>
 *   <li>a glottal pulse / square / noise excitation,</li>
 *   <li>driven through a cascade of four time-varying formant resonators,</li>
 *   <li>shaped by a tilt low pass and a DC blocker,</li>
 *   <li>normalised and soft limited to 16 bit PCM.</li>
 * </ol>
 *
 * <p>Everything is plain Java, so it runs on any machine without a network connection and can be
 * exercised by unit tests. The voice character is controlled purely by the fundamental frequency,
 * the vocal tract scale, breathiness, vibrato, jitter and the excitation waveform.</p>
 */
public final class SpeechSynthesizer
{
    /** Relative f0 contours per Mandarin tone (index 0 = sentence neutral). */
    private static final float[][] TONE_CONTOURS = {
        {1.00F, 0.96F},
        {1.00F, 1.00F},
        {0.90F, 1.16F},
        {0.84F, 0.74F, 1.05F},
        {1.17F, 0.78F},
        {0.97F, 0.93F},
    };

    private SpeechSynthesizer()
    {}

    private static class Frame
    {
        int samples;
        float f1;
        float f2;
        float f3;
        float voiced;
        float noise;
        float f0a;
        float f0b;
        float[] contour;
        int contourIndex;
    }

    /** Two pole resonator with unity DC gain. */
    private static class Resonator
    {
        private float a0;
        private float b1;
        private float b2;
        private float y1;
        private float y2;

        void set(float frequency, float bandWidth, float sampleRate)
        {
            float nyquist = sampleRate * 0.5F;

            if (frequency <= 0F || frequency >= nyquist * 0.96F)
            {
                this.a0 = 0F;
                this.b1 = 0F;
                this.b2 = 0F;

                return;
            }

            float r = (float) Math.exp(-Math.PI * bandWidth / sampleRate);
            float theta = (float) (2D * Math.PI * frequency / sampleRate);

            this.b1 = 2F * r * (float) Math.cos(theta);
            this.b2 = -r * r;
            this.a0 = 1F - this.b1 - this.b2;
        }

        float process(float x)
        {
            float y = this.a0 * x + this.b1 * this.y1 + this.b2 * this.y2;

            if (y > 12F)
            {
                y = 12F;
            }
            else if (y < -12F)
            {
                y = -12F;
            }

            this.y2 = this.y1;
            this.y1 = y;

            return y;
        }
    }

    public static short[] render(VoiceRequest request, List<Phone> phones)
    {
        if (phones == null || phones.isEmpty())
        {
            return new short[0];
        }

        int sampleRate = Math.max(8000, request.sampleRate);
        List<Frame> frames = buildFrames(request, phones, sampleRate);

        int total = 0;

        for (Frame frame : frames)
        {
            total += frame.samples;
        }

        if (total <= 0)
        {
            return new short[0];
        }

        float[] buffer = new float[total];

        synthesize(request, frames, sampleRate, buffer);
        postProcess(buffer, sampleRate);

        short[] pcm = new short[total];

        for (int i = 0; i < total; i++)
        {
            float value = buffer[i];

            if (value > 1F)
            {
                value = 1F;
            }
            else if (value < -1F)
            {
                value = -1F;
            }

            pcm[i] = (short) Math.round(value * 32000F);
        }

        return pcm;
    }

    private static List<Frame> buildFrames(VoiceRequest request, List<Phone> phones, int sampleRate)
    {
        List<Frame> frames = new ArrayList<>();

        float baseF0 = Math.max(40F, Math.min(600F, request.f0));
        float formantScale = Math.max(0.5F, request.formant);
        float speed = 1F / Math.max(0.25F, request.pace);
        float msToSamples = sampleRate / 1000F;

        for (Phone phone : phones)
        {
            Phoneme phoneme = phone.phoneme;
            float scale = Math.max(0.15F, phone.durationScale) * speed;
            float totalMs = 0F;

            for (Phoneme.Part part : phoneme.parts)
            {
                totalMs += part.duration * scale;
            }

            int totalSamples = Math.max(1, Math.round(totalMs * msToSamples));
            float[] contour = null;

            if (phone.nucleus && phoneme.kind == Phoneme.Kind.VOWEL)
            {
                int tone = Math.max(0, Math.min(5, phone.tone));
                contour = request.tone || tone == 0 ? TONE_CONTOURS[tone] : TONE_CONTOURS[0];
            }

            if (contour != null && phoneme.parts.length == 1)
            {
                Phoneme.Part part = phoneme.parts[0];
                int parts = contour.length;
                int per = Math.max(1, totalSamples / parts);

                for (int k = 0; k < parts; k++)
                {
                    int samples = k == parts - 1 ? totalSamples - per * (parts - 1) : per;

                    if (samples <= 0)
                    {
                        continue;
                    }

                    Frame frame = new Frame();

                    frame.samples = samples;
                    frame.f1 = part.f1 * formantScale;
                    frame.f2 = part.f2 * formantScale;
                    frame.f3 = part.f3 * formantScale;
                    frame.voiced = part.voiced;
                    frame.noise = part.noise;
                    frame.contour = contour;
                    frame.contourIndex = k;

                    frames.add(frame);
                }
            }
            else
            {
                for (Phoneme.Part part : phoneme.parts)
                {
                    Frame frame = new Frame();

                    frame.samples = Math.max(1, Math.round(part.duration * scale * msToSamples));
                    frame.f1 = part.f1 * formantScale;
                    frame.f2 = part.f2 * formantScale;
                    frame.f3 = part.f3 * formantScale;
                    frame.voiced = part.voiced;
                    frame.noise = part.noise;

                    frames.add(frame);
                }
            }
        }

        /* Assign the f0 track: every frame ramps from the previous nucleus' ending multiplier. */
        float multiplier = 1F;

        for (Frame frame : frames)
        {
            if (frame.contour != null)
            {
                int index = frame.contourIndex;
                float from = index == 0 ? multiplier : frame.contour[index - 1];
                float to = frame.contour[index];

                frame.f0a = baseF0 * from;
                frame.f0b = baseF0 * to;
                multiplier = to;
            }
            else
            {
                frame.f0a = baseF0 * multiplier;
                frame.f0b = baseF0 * multiplier;
            }
        }

        return frames;
    }

    private static void synthesize(VoiceRequest request, List<Frame> frames, int sampleRate, float[] out)
    {
        Resonator[] resonators = new Resonator[] {new Resonator(), new Resonator(), new Resonator(), new Resonator()};

        Random random = new Random(0x5eed5eedL ^ request.signature().hashCode());

        float smoothing = (float) (1D - Math.exp(-1D / (0.018D * sampleRate)));
        float breath = Math.max(0F, Math.min(1F, request.breath));
        float vibrato = Math.max(0F, request.vibrato);
        float jitter = Math.max(0F, request.jitter);
        float tilt = Math.max(0F, Math.min(1F, request.tilt));

        /* The source is pre-emphasised above, so this shelf only has to shave the very top; the old
           1400 Hz base also swallowed the sibilants, which live at 4-8 kHz. */
        float tiltCutoff = 3200F + tilt * 6800F;
        float lowPassK = (float) (1D - Math.exp(-2D * Math.PI * tiltCutoff / sampleRate));

        float cf1 = 500F;
        float cf2 = 1500F;
        float cf3 = 2500F;

        double phase = 0D;
        double increment = 1D / sampleRate * 120D;
        float previousPulse = 0F;
        float previousExcitation = 0F;
        float lowPass = 0F;
        float previousIn = 0F;
        float previousOut = 0F;

        boolean noiseOnly = request.excitation == 2;
        boolean square = request.excitation == 1;

        int index = 0;

        for (Frame frame : frames)
        {
            float target1 = frame.f1;
            float target2 = frame.f2;
            float target3 = frame.f3;

            for (int i = 0; i < frame.samples; i++)
            {
                float progress = (i + 1F) / frame.samples;

                cf1 += (target1 - cf1) * smoothing;
                cf2 += (target2 - cf2) * smoothing;
                cf3 += (target3 - cf3) * smoothing;

                if ((i & 31) == 0)
                {
                    float f4 = Math.max(3200F, cf3 + 900F);

                    resonators[0].set(cf1, 70F + cf1 * 0.06F, sampleRate);
                    resonators[1].set(cf2, 92F + cf2 * 0.05F, sampleRate);
                    resonators[2].set(cf3, 130F + cf3 * 0.04F, sampleRate);
                    resonators[3].set(f4, 220F, sampleRate);
                }

                float f0 = frame.f0a + (frame.f0b - frame.f0a) * progress;

                /* sentence declination */
                f0 *= 1F - 0.16F * (index / (float) out.length);

                if (vibrato > 0F)
                {
                    f0 *= 1F + vibrato * (float) Math.sin(2D * Math.PI * 5.4D * index / sampleRate);
                }

                if (phase >= 1D)
                {
                    phase -= 1D;

                    float jittered = f0 * (1F + jitter * (random.nextFloat() * 2F - 1F));

                    increment = Math.max(1D, jittered) / sampleRate;
                }

                phase += increment;

                float glottal;

                if (square)
                {
                    glottal = phase < 0.5D ? 0.8F : -0.8F;
                }
                else
                {
                    float open = 0.30F;
                    float close = open + 0.12F;
                    float pulse;

                    if (phase < open)
                    {
                        float x = (float) (phase / open);

                        pulse = 3F * x * x - 2F * x * x * x;
                    }
                    else if (phase < close)
                    {
                        float x = (float) ((phase - open) / (close - open));

                        pulse = 1F - x * x;
                    }
                    else
                    {
                        pulse = 0F;
                    }

                    glottal = pulse - previousPulse;
                    previousPulse = pulse;
                }

                float noise = random.nextFloat() * 2F - 1F;
                float voicedAmplitude = frame.voiced;
                float noiseAmplitude = frame.noise;

                if (noiseOnly)
                {
                    noiseAmplitude += voicedAmplitude * 0.85F;
                    voicedAmplitude = 0F;
                }

                float excitation = voicedAmplitude * glottal * 0.55F;

                excitation += breath * voicedAmplitude * noise * 0.30F;
                excitation += noiseAmplitude * noise * 0.40F;

                /* Lip radiation (+6 dB/oct). The glottal pulse on its own already loses ~12 dB/oct,
                   so without this the harmonics above ~1 kHz are far too weak to excite F2/F3 at
                   all: a lone "i" then measures 97% of its energy below 500 Hz and sounds like a
                   growl instead of a vowel. This is what makes the front vowels ring. */
                float radiated = excitation - previousExcitation;

                previousExcitation = excitation;
                excitation = excitation * 0.10F + radiated * 0.90F;

                float value = excitation;

                for (Resonator resonator : resonators)
                {
                    value = resonator.process(value);
                }

                lowPass += (value - lowPass) * lowPassK;

                float mixed = lowPass * (0.45F + 0.55F * tilt) + value * (0.55F - 0.55F * tilt);

                /* DC blocker */
                float filtered = mixed - previousIn + 0.9995F * previousOut;

                previousIn = mixed;
                previousOut = filtered;

                out[index] = filtered;
                index += 1;
            }
        }
    }

    private static void postProcess(float[] buffer, int sampleRate)
    {
        /* de-click fades */
        int fade = Math.max(1, Math.round(0.012F * sampleRate));

        for (int i = 0; i < fade && i < buffer.length; i++)
        {
            buffer[i] *= i / (float) fade;
            buffer[buffer.length - 1 - i] *= i / (float) fade;
        }

        float peak = 0F;
        double squares = 0D;

        for (float value : buffer)
        {
            float absolute = Math.abs(value);

            if (absolute > peak)
            {
                peak = absolute;
            }

            squares += (double) value * value;
        }

        if (peak <= 1.0E-6F)
        {
            return;
        }

        float rms = (float) Math.sqrt(squares / buffer.length);
        float gain = 0.95F / peak;

        /* lift quiet utterances a bit, but never past the peak headroom */
        if (rms > 1.0E-6F && rms * gain < 0.11F)
        {
            gain *= Math.min(2.2F, 0.11F / (rms * gain));
        }

        for (int i = 0; i < buffer.length; i++)
        {
            float value = buffer[i] * gain;

            /* soft knee limiter */
            if (value > 0.82F || value < -0.82F)
            {
                value = (float) Math.tanh(value);
            }

            buffer[i] = value;
        }
    }
}
