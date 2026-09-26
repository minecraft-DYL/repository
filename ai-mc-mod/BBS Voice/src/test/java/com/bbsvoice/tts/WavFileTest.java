package com.bbsvoice.tts;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

import java.io.File;
import java.nio.file.Path;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class WavFileTest
{
    @Test
    void roundTripsSixteenBitMono(@TempDir Path folder) throws Exception
    {
        short[] samples = new short[1024];

        for (int i = 0; i < samples.length; i++)
        {
            samples[i] = (short) (Math.sin(i / 16D) * 12000D);
        }

        File file = folder.resolve("test.wav").toFile();

        WavFile.write(file, samples, 22050);

        assertTrue(file.length() > 44, "a wave header plus data should be written");

        short[] read = WavFile.read(file);

        assertArrayEquals(samples, read, "BBS' WaveReader must be able to read what we write");
    }

    @Test
    void writesAValidRiffHeader(@TempDir Path folder) throws Exception
    {
        short[] samples = new short[] {0, 1000, -1000, 2000};

        File file = folder.resolve("header.wav").toFile();

        WavFile.write(file, samples, 44100);

        byte[] bytes = java.nio.file.Files.readAllBytes(file.toPath());

        assertEquals("RIFF", new String(bytes, 0, 4, "US-ASCII"));
        assertEquals("WAVE", new String(bytes, 8, 4, "US-ASCII"));
        assertEquals("fmt ", new String(bytes, 12, 4, "US-ASCII"));
        assertEquals("data", new String(bytes, 36, 4, "US-ASCII"));
        assertEquals(44100, (bytes[24] & 0xff) | ((bytes[25] & 0xff) << 8) | ((bytes[26] & 0xff) << 16) | ((bytes[27] & 0xff) << 24));
        assertEquals(samples.length * 2, bytes.length - 44);
    }
}
