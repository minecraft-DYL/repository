import javax.imageio.ImageIO;
import javax.sound.sampled.AudioFormat;
import javax.sound.sampled.AudioInputStream;
import javax.sound.sampled.AudioSystem;
import java.awt.image.BufferedImage;
import java.io.File;

/**
 * Objective look at one of the generated wav files: header fields, level/DC, and a per block
 * RMS / zero crossing / pitch estimate plus a spectrogram PNG so a human (or a vision model)
 * can see whether the thing actually looks like speech.
 *
 * usage: java tools/AnalyzeWave.java <wav> [spectrogram.png]
 */
public class AnalyzeWave
{
    public static void main(String[] args) throws Exception
    {
        File file = new File(args[0]);
        File png = new File(args.length > 1 ? args[1] : "build/spectrogram.png");

        AudioInputStream in = AudioSystem.getAudioInputStream(file);
        AudioFormat fmt = in.getFormat();
        byte[] raw = in.readAllBytes();

        int channels = fmt.getChannels();
        float rate = fmt.getSampleRate();
        int bits = fmt.getSampleSizeInBits();
        int bytes = bits / 8;
        int frames = raw.length / (bytes * channels);

        System.out.println("file      : " + file + "  (" + file.length() + " bytes)");
        System.out.println("format    : " + fmt);
        System.out.println("frames    : " + frames + "  duration " + String.format("%.3f", frames / rate) + "s");

        float[] mono = new float[frames];

        for (int i = 0; i < frames; i++)
        {
            int index = i * bytes * channels;

            if (bits == 16)
            {
                mono[i] = (short) ((raw[index] & 0xFF) | (raw[index + 1] << 8)) / 32768F;
            }
            else if (bits == 8)
            {
                mono[i] = ((raw[index] & 0xFF) - 128) / 128F;
            }
        }

        double sum = 0D;
        double peak = 0D;
        int silentBlocks = 0;

        for (float v : mono)
        {
            sum += v * v;
            peak = Math.max(peak, Math.abs(v));
        }

        System.out.println("peak      : " + String.format("%.4f", peak)
            + "   rms " + String.format("%.5f", Math.sqrt(sum / Math.max(1, frames)))
            + "   dc " + String.format("%.5f", mean(mono)));

        int block = (int) (rate * 0.1);
        System.out.println("blocks    : 100ms rms / zcr / pitch");

        StringBuilder line = new StringBuilder();

        for (int start = 0; start + block <= mono.length; start += block)
        {
            double rms = 0D;
            int crossings = 0;

            for (int i = start; i < start + block; i++)
            {
                rms += mono[i] * mono[i];

                if (i > start && (mono[i] >= 0) != (mono[i - 1] >= 0))
                {
                    crossings++;
                }
            }

            rms = Math.sqrt(rms / block);

            if (rms < 0.002D)
            {
                silentBlocks++;
            }

            float pitch = estimatePitch(mono, start, block, (int) rate);
            line.append(String.format("[%.1fs r=%.3f z=%4d p=%5.0f] ", start / rate, rms, crossings, pitch));
        }

        System.out.println(line);
        System.out.println("silent 100ms blocks: " + silentBlocks);
        System.out.println("clipped samples    : " + clipped(mono));

        spectrogram(mono, (int) rate, png);
        System.out.println("spectrogram        : " + png.getAbsolutePath());
    }

    private static int clipped(float[] samples)
    {
        int count = 0;

        for (float v : samples)
        {
            if (Math.abs(v) > 0.9995F)
            {
                count++;
            }
        }

        return count;
    }

    private static double mean(float[] samples)
    {
        double sum = 0D;

        for (float v : samples)
        {
            sum += v;
        }

        return sum / Math.max(1, samples.length);
    }

    /** Autocorrelation pitch estimate, 60..500 Hz. */
    private static float estimatePitch(float[] samples, int start, int length, int rate)
    {
        int min = rate / 500;
        int max = rate / 60;
        float best = 0F;
        int bestLag = 0;
        double energy = 0D;

        for (int i = start; i < start + length; i++)
        {
            energy += samples[i] * samples[i];
        }

        if (energy < 1E-4D)
        {
            return 0F;
        }

        for (int lag = min; lag <= max && lag < length / 2; lag++)
        {
            double sum = 0D;

            for (int i = start; i < start + length - lag; i++)
            {
                sum += samples[i] * samples[i + lag];
            }

            float value = (float) (sum / energy);

            if (value > best)
            {
                best = value;
                bestLag = lag;
            }
        }

        return bestLag == 0 ? 0F : (float) rate / bestLag;
    }

    /** Simple magnitude spectrogram: 256 Hz wide bins, dark = quiet. */
    private static void spectrogram(float[] samples, int rate, File out) throws Exception
    {
        int fft = 512;
        int hop = fft / 4;
        int bands = 48;
        int cols = Math.max(1, (samples.length - fft) / hop);
        BufferedImage image = new BufferedImage(cols, bands, BufferedImage.TYPE_INT_RGB);
        double[] real = new double[fft];
        double[] imag = new double[fft];
        double[][] mag = new double[bands][cols];
        double max = 0D;

        for (int c = 0; c < cols; c++)
        {
            int start = c * hop;

            for (int i = 0; i < fft; i++)
            {
                double window = 0.5D - 0.5D * Math.cos(2D * Math.PI * i / (fft - 1));
                real[i] = samples[start + i] * window;
                imag[i] = 0D;
            }

            fft(real, imag);

            for (int b = 0; b < bands; b++)
            {
                /* log spaced from 60 Hz to 8 kHz */
                double from = 60D * Math.pow(8000D / 60D, b / (double) bands);
                double to = 60D * Math.pow(8000D / 60D, (b + 1) / (double) bands);
                int i0 = Math.max(1, (int) (from * fft / rate));
                int i1 = Math.min(fft / 2 - 1, Math.max(i0, (int) (to * fft / rate)));
                double sum = 0D;

                for (int k = i0; k <= i1; k++)
                {
                    sum += Math.hypot(real[k], imag[k]);
                }

                mag[b][c] = Math.log1p(sum / (i1 - i0 + 1) * 100D);
                max = Math.max(max, mag[b][c]);
            }
        }

        for (int c = 0; c < cols; c++)
        {
            for (int b = 0; b < bands; b++)
            {
                int v = (int) Math.min(255D, mag[b][c] / max * 255D);
                int rgb = (v << 16) | (v << 8) | v;
                image.setRGB(c, bands - 1 - b, rgb);
            }
        }

        ImageIO.write(image, "png", out);
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
