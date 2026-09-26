import javax.imageio.ImageIO;
import javax.sound.sampled.AudioFormat;
import javax.sound.sampled.AudioInputStream;
import javax.sound.sampled.AudioSystem;
import java.awt.image.BufferedImage;
import java.io.File;

/**
 * Dev tool: linear frequency spectrogram (0..maxHz) for eyeballing formant tracks.
 * usage: java tools/SpecImage.java <wav> <out.png> [maxHz]
 */
public class SpecImage
{
    public static void main(String[] args) throws Exception
    {
        File file = new File(args[0]);
        File out = new File(args[1]);
        int maxHz = args.length > 2 ? Integer.parseInt(args[2]) : 5000;

        AudioInputStream in = AudioSystem.getAudioInputStream(file);
        AudioFormat fmt = in.getFormat();
        byte[] raw = in.readAllBytes();
        int rate = (int) fmt.getSampleRate();
        int frames = raw.length / 2;
        float[] mono = new float[frames];

        for (int i = 0; i < frames; i++)
        {
            mono[i] = (short) ((raw[i * 2] & 0xFF) | (raw[i * 2 + 1] << 8)) / 32768F;
        }

        int fft = 512;
        int hop = 128;
        int height = 400;
        int cols = Math.max(1, (frames - fft) / hop);
        BufferedImage image = new BufferedImage(cols, height, BufferedImage.TYPE_INT_RGB);
        double[] real = new double[fft];
        double[] imag = new double[fft];
        double[][] mag = new double[cols][height];
        double max = 1E-9D;

        for (int c = 0; c < cols; c++)
        {
            int start = c * hop;

            for (int i = 0; i < fft; i++)
            {
                double window = 0.5D - 0.5D * Math.cos(2D * Math.PI * i / (fft - 1));
                real[i] = mono[start + i] * window;
                imag[i] = 0D;
            }

            fft(real, imag);

            for (int y = 0; y < height; y++)
            {
                double hz = maxHz * (1D - y / (double) height);
                int k = (int) Math.round(hz * fft / rate);
                double v = 0D;

                if (k > 0 && k < fft / 2)
                {
                    v = Math.hypot(real[k], imag[k]);
                }

                mag[c][y] = v;
                max = Math.max(max, v);
            }
        }

        for (int c = 0; c < cols; c++)
        {
            for (int y = 0; y < height; y++)
            {
                double norm = Math.pow(mag[c][y] / max, 0.4D);
                int v = (int) Math.min(255D, norm * 255D);
                image.setRGB(c, y, (v << 16) | (v << 8) | v);
            }
        }

        ImageIO.write(image, "png", out);
        System.out.println("wrote " + out.getAbsolutePath() + "  " + cols + "x" + height + "  maxHz=" + maxHz);
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
