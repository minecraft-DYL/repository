import javax.sound.sampled.AudioFormat;
import javax.sound.sampled.AudioInputStream;
import javax.sound.sampled.AudioSystem;
import java.io.File;
import java.util.ArrayList;
import java.util.List;

/**
 * Spectrum view of one generated wav: where is the energy, what is the fundamental and where are
 * the formant peaks. This is how you tell "formant synthesis with wrong targets" (energy stuck in
 * the low end = a growling monster) from a healthy vowel (f0 ~100-200 Hz, formants at 300-3000 Hz).
 *
 * usage: java tools/AnalyzeSpectrum.java <wav> [windows]
 */
public class AnalyzeSpectrum
{
    public static void main(String[] args) throws Exception
    {
        File file = new File(args[0]);
        int windows = args.length > 1 ? Integer.parseInt(args[1]) : 3;

        AudioInputStream in = AudioSystem.getAudioInputStream(file);
        AudioFormat fmt = in.getFormat();
        byte[] raw = in.readAllBytes();
        int rate = (int) fmt.getSampleRate();
        int frameBytes = fmt.getSampleSizeInBits() / 8 * fmt.getChannels();
        int frames = raw.length / frameBytes;
        float[] mono = new float[frames];

        for (int i = 0; i < frames; i++)
        {
            int index = i * frameBytes;
            mono[i] = (short) ((raw[index] & 0xFF) | (raw[index + 1] << 8)) / 32768F;
        }

        int size = 4096;
        int span = 2048;

        if (frames < size)
        {
            size = Integer.highestOneBit(Math.max(256, frames));
            span = Math.max(1, size / 2);
        }

        List<int[]> starts = new ArrayList<>();

        for (int start = 0; start + size <= frames; start += span)
        {
            starts.add(new int[] {start, (int) (rms(mono, start, size) * 1.0E6F)});
        }

        starts.sort((a, b) -> Integer.compare(b[1], a[1]));

        int used = 0;

        System.out.println("file: " + file.getName() + "  rate " + rate + "  duration " + String.format("%.3f", frames / (float) rate) + "s");
        System.out.println("     time   f0(Hz)   low<500   mid 500-4k   high>4k | formant-ish peaks (Hz, dB rel)");

        for (int[] candidate : starts)
        {
            if (used++ >= windows)
            {
                break;
            }

            int start = candidate[0];
            double[] real = new double[size];
            double[] imag = new double[size];

            for (int i = 0; i < size; i++)
            {
                double window = 0.5D - 0.5D * Math.cos(2D * Math.PI * i / (size - 1));
                real[i] = mono[start + i] * window;
            }

            fft(real, imag);

            double[] mag = new double[size / 2];

            for (int k = 0; k < size / 2; k++)
            {
                mag[k] = Math.hypot(real[k], imag[k]);
            }

            double total = 0D;
            double low = 0D;
            double mid = 0D;
            double high = 0D;
            double[] bands = new double[8];

            for (int k = 1; k < mag.length; k++)
            {
                double energy = mag[k] * mag[k];
                double hz = k * (double) rate / size;

                total += energy;

                if (hz < 500D)
                {
                    low += energy;
                }
                else if (hz < 4000D)
                {
                    mid += energy;
                }
                else
                {
                    high += energy;
                }

                for (int b = 0; b < bands.length; b++)
                {
                    double from = 62.5D * Math.pow(2D, b - 1);

                    if (hz >= from && hz < from * 2D)
                    {
                        bands[b] += energy;
                    }
                }
            }

            double f0 = hps(mag, rate, size);
            List<int[]> peaks = peaks(mag, rate, size);
            StringBuilder sb = new StringBuilder();

            for (int[] peak : peaks)
            {
                sb.append(String.format("%.0f/%.0f ", peak[0] * rate / (double) size, 20D * Math.log10(peak[1] / Math.max(1E-9D, mag[1]))));
            }

            System.out.println(String.format("%8.2fs %7.1f %8.1f%% %10.1f%% %9.1f%% | %s",
                start / (double) rate, f0,
                low / total * 100D, mid / total * 100D, high / total * 100D, sb.toString().trim()));

            StringBuilder oct = new StringBuilder();

            for (int b = 0; b < bands.length; b++)
            {
                oct.append(String.format("%.0f-%.0fHz %4.1f%%  ", 62.5D * Math.pow(2D, b - 1), 62.5D * Math.pow(2D, b),
                    bands[b] / total * 100D));
            }

            System.out.println("           octave bands: " + oct.toString().trim());
        }
    }

    private static float rms(float[] samples, int start, int length)
    {
        double sum = 0D;

        for (int i = start; i < start + length; i++)
        {
            sum += samples[i] * samples[i];
        }

        return (float) Math.sqrt(sum / length);
    }

    /** Harmonic product spectrum, 50..400 Hz. */
    private static double hps(double[] mag, int rate, int size)
    {
        double best = -1E18D;
        int bestHz = 0;

        for (int hz = 50; hz <= 400; hz++)
        {
            double sum = 0D;

            for (int k = 1; k <= 6; k++)
            {
                int bin = (int) Math.round(hz * k * (double) size / rate);

                if (bin <= 0 || bin >= mag.length - 1)
                {
                    continue;
                }

                double value = Math.max(mag[bin - 1], Math.max(mag[bin], mag[bin + 1]));
                sum += Math.log10(value + 1E-9D);
            }

            if (sum > best)
            {
                best = sum;
                bestHz = hz;
            }
        }

        return bestHz;
    }

    /** Top local maxima below 6 kHz, at least 200 Hz apart. */
    private static List<int[]> peaks(double[] mag, int rate, int size)
    {
        List<int[]> found = new ArrayList<>();
        double max = 0D;

        for (double value : mag)
        {
            max = Math.max(max, value);
        }

        for (int pass = 0; pass < 6; pass++)
        {
            int bestBin = -1;
            double bestValue = 0.05D * max;

            for (int k = 48; k < Math.min(mag.length - 1, (int) (6000D * size / rate)); k++)
            {
                boolean local = mag[k] >= mag[k - 1] && mag[k] >= mag[k + 1];

                if (!local)
                {
                    continue;
                }

                boolean close = false;

                for (int[] peak : found)
                {
                    if (Math.abs(peak[0] - k) * rate / size < 200D)
                    {
                        close = true;
                        break;
                    }
                }

                if (!close && mag[k] > bestValue)
                {
                    bestValue = mag[k];
                    bestBin = k;
                }
            }

            if (bestBin < 0)
            {
                break;
            }

            found.add(new int[] {bestBin, (int) (mag[bestBin] * 1.0E6D)});
        }

        return found;
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
