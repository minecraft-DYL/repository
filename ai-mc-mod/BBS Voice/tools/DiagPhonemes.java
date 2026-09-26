import com.bbsvoice.tts.*;

import java.util.ArrayList;
import java.util.List;

/**
 * Dev tool: renders every phoneme in the registry in isolation and reports where its energy
 * actually lands (band split + strongest spectral peaks below 4 kHz) so a vowel that is being
 * "starved" into a rumble (intended formant far from the measured peak) shows up immediately.
 *
 * usage: java -cp <classes>;src/main/resources;tools tools/DiagPhonemes.java
 */
public class DiagPhonemes
{
    public static void main(String[] args) throws Exception
    {
        for (Phoneme phoneme : Phoneme.registry().values())
        {
            List<Phone> phones = new ArrayList<>();
            phones.add(new Phone(phoneme, 0, phoneme.nucleus, 1F));

            VoiceRequest request = new VoiceRequest();
            request.text = phoneme.id;
            request.sampleRate = 22050;
            request.f0 = 120F;
            request.formant = 1F;
            request.breath = 0.06F;
            request.vibrato = 0F;
            request.jitter = 0F;
            request.tilt = 0.45F;
            request.excitation = 0;
            request.tone = false;

            short[] pcm = SpeechSynthesizer.render(request, phones);

            if (pcm.length < 256)
            {
                System.out.printf("%-8s (too short)%n", phoneme.id);
                continue;
            }

            int n = 1;
            while (n < pcm.length)
            {
                n <<= 1;
            }

            n = Math.min(n, 16384);

            double[] real = new double[n];
            double[] imag = new double[n];
            int base = Math.max(0, (pcm.length - n) / 2);

            for (int i = 0; i < n && base + i < pcm.length; i++)
            {
                double window = 0.5D - 0.5D * Math.cos(2D * Math.PI * i / (n - 1));
                real[i] = pcm[base + i] / 32768D * window;
            }

            fft(real, imag);

            double rate = 22050D;
            double[] bandEdges = {0D, 300D, 800D, 1500D, 2500D, 4000D, 11025D};
            double[] band = new double[bandEdges.length - 1];
            double total = 0D;

            for (int k = 1; k < n / 2; k++)
            {
                double hz = k * rate / n;
                double energy = real[k] * real[k] + imag[k] * imag[k];

                total += energy;

                for (int b = 0; b < band.length; b++)
                {
                    if (hz >= bandEdges[b] && hz < bandEdges[b + 1])
                    {
                        band[b] += energy;
                    }
                }
            }

            StringBuilder peaks = new StringBuilder();
            boolean[] used = new boolean[n / 2];

            for (int p = 0; p < 3; p++)
            {
                int best = -1;
                double bestValue = 0D;

                for (int k = 3; k < Math.min(n / 2 - 1, (int) (4000D * n / rate)); k++)
                {
                    if (used[k])
                    {
                        continue;
                    }

                    double value = real[k] * real[k] + imag[k] * imag[k];

                    if (value > bestValue)
                    {
                        bestValue = value;
                        best = k;
                    }
                }

                if (best < 0)
                {
                    break;
                }

                used[best] = true;

                for (int k = Math.max(3, best - 12); k <= Math.min(n / 2 - 1, best + 12); k++)
                {
                    used[k] = true;
                }

                peaks.append(String.format("%.0fHz(%d%%) ", best * rate / n, Math.round(bestValue / Math.max(1E-12D, total) * 100D)));
            }

            /* Vertain F1/F2/F3 from the phoneme so we can see the mismatch. */
            StringBuilder declared = new StringBuilder();

            for (Phoneme.Part part : phoneme.parts)
            {
                declared.append(String.format("%.0f/%.0f/%.0f ", part.f1, part.f2, part.f3));
            }

            System.out.printf("%-8s dur=%4dms bands[<300 %4.1f%% 300-800 %4.1f%% 800-1.5k %4.1f%% 1.5-2.5k %4.1f%% 2.5-4k %4.1f%% >4k %4.1f%%] peaks: %-30s declared %s%n",
                phoneme.id, pcm.length * 1000 / 22050,
                band[0] / total * 100D, band[1] / total * 100D, band[2] / total * 100D,
                band[3] / total * 100D, band[4] / total * 100D, band[5] / total * 100D,
                peaks.toString().trim(), declared.toString().trim());
        }
    }

    private static void fft(double[] real, double[] imag)
    {
        int n = real.length;

        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;

            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;

            if (i < j)
            {
                double t = real[i];
                real[i] = real[j];
                real[j] = t;
                t = imag[i];
                imag[i] = imag[j];
                imag[j] = t;
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2D * Math.PI / len;
            double wr = Math.cos(angle);
            double wi = Math.sin(angle);

            for (int i = 0; i < n; i += len)
            {
                double cr = 1D;
                double ci = 0D;

                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k;
                    int b = i + k + len / 2;
                    double xr = real[b] * cr - imag[b] * ci;
                    double xi = real[b] * ci + imag[b] * cr;

                    real[b] = real[a] - xr;
                    imag[b] = imag[a] - xi;
                    real[a] += xr;
                    imag[a] += xi;

                    double nr = cr * wr - ci * wi;
                    ci = cr * wi + ci * wr;
                    cr = nr;
                }
            }
        }
    }
}
