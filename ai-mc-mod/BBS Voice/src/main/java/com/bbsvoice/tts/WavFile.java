package com.bbsvoice.tts;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.OutputStream;

/**
 * Minimal, dependency free 16 bit PCM WAV writer/reader. BBS' own {@code WaveReader} understands
 * exactly this layout (RIFF / WAVE / fmt / data), so the rendered cache files load straight into
 * BBS' sound manager.
 */
public final class WavFile
{
    private WavFile()
    {}

    public static void write(File file, short[] samples, int sampleRate) throws IOException
    {
        try (FileOutputStream stream = new FileOutputStream(file))
        {
            write(stream, samples, sampleRate);
        }
    }

    public static void write(OutputStream stream, short[] samples, int sampleRate) throws IOException
    {
        int dataLength = samples.length * 2;
        int byteRate = sampleRate * 2;

        stream.write("RIFF".getBytes("US-ASCII"));
        writeIntLE(stream, 36 + dataLength);
        stream.write("WAVE".getBytes("US-ASCII"));

        stream.write("fmt ".getBytes("US-ASCII"));
        writeIntLE(stream, 16);
        writeShortLE(stream, 1);            /* PCM */
        writeShortLE(stream, 1);            /* mono */
        writeIntLE(stream, sampleRate);
        writeIntLE(stream, byteRate);
        writeShortLE(stream, 2);            /* block align */
        writeShortLE(stream, 16);           /* bits per sample */

        stream.write("data".getBytes("US-ASCII"));
        writeIntLE(stream, dataLength);

        byte[] buffer = new byte[Math.max(2, samples.length * 2)];
        int i = 0;

        for (short sample : samples)
        {
            buffer[i] = (byte) (sample & 0xff);
            buffer[i + 1] = (byte) ((sample >> 8) & 0xff);
            i += 2;
        }

        stream.write(buffer);
        stream.flush();
    }

    /** Reads a simple 16 bit mono/stereo PCM wav back into mono samples (used by the tests). */
    public static short[] read(File file) throws IOException
    {
        byte[] bytes = java.nio.file.Files.readAllBytes(file.toPath());

        if (bytes.length < 44)
        {
            return new short[0];
        }

        int channels = bytes[22] | (bytes[23] << 8);
        int bits = bytes[34] | (bytes[35] << 8);
        int offset = 12;

        while (offset + 8 <= bytes.length)
        {
            String id = new String(bytes, offset, 4, "US-ASCII");
            int size = (bytes[offset + 4] & 0xff) | ((bytes[offset + 5] & 0xff) << 8)
                | ((bytes[offset + 6] & 0xff) << 16) | ((bytes[offset + 7] & 0xff) << 24);

            if (id.equals("data"))
            {
                offset += 8;

                int frames = Math.min(size, bytes.length - offset) / (channels * (bits / 8));
                short[] out = new short[frames];

                for (int i = 0; i < frames; i++)
                {
                    int index = offset + i * channels * (bits / 8);
                    out[i] = (short) ((bytes[index] & 0xff) | (bytes[index + 1] << 8));
                }

                return out;
            }

            offset += 8 + size + (size & 1);
        }

        return new short[0];
    }

    private static void writeIntLE(OutputStream stream, int value) throws IOException
    {
        ByteArrayOutputStream buffer = new ByteArrayOutputStream(4);

        buffer.write(value & 0xff);
        buffer.write((value >> 8) & 0xff);
        buffer.write((value >> 16) & 0xff);
        buffer.write((value >> 24) & 0xff);
        buffer.writeTo(stream);
    }

    private static void writeShortLE(OutputStream stream, int value) throws IOException
    {
        stream.write(value & 0xff);
        stream.write((value >> 8) & 0xff);
    }
}
