package com.bbsvoice.client.voice;

import com.bbsvoice.BBSVoice;
import com.bbsvoice.BBSVoiceSettings;
import com.bbsvoice.tts.VoiceRequest;
import com.bbsvoice.tts.VoiceSynthesis;
import com.bbsvoice.tts.WavFile;
import com.bbsvoice.voice.VoiceData;
import mchorse.bbs_mod.BBSMod;
import mchorse.bbs_mod.resources.Link;
import net.minecraft.client.MinecraftClient;

import java.io.File;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Renders utterances in the background and caches them as {@code .wav} files under
 * {@code config/bbs/assets/audio/bbsvoice/}.
 *
 * <p>The cache key is the signature of everything that changes the audio (text and voice character),
 * so a clip only ever renders once per unique line. Volume, speed and pitch are applied live during
 * playback and therefore never invalidate the cache.</p>
 */
public final class VoiceAudioCache
{
    public static final String FOLDER = BBSVoice.VOICE_CACHE_FOLDER;

    public enum Status
    {
        EMPTY,
        RENDERING,
        READY,
        FAILED
    }

    private static final Map<String, Link> READY = new ConcurrentHashMap<>();
    private static final Set<String> PENDING = ConcurrentHashMap.newKeySet();
    private static final Set<String> FAILED = ConcurrentHashMap.newKeySet();

    private static final ExecutorService POOL = Executors.newSingleThreadExecutor((runnable) ->
    {
        Thread thread = new Thread(runnable, "BBSVoice-TTS");

        thread.setDaemon(true);
        thread.setPriority(Thread.MIN_PRIORITY + 1);

        return thread;
    });

    private VoiceAudioCache()
    {}

    public static String resolveSignature(VoiceData data)
    {
        return data.toRequest(BBSVoiceSettings.sampleRate()).signature();
    }

    /** @return the playable link, or {@code null} while it is still being rendered. */
    public static Link get(VoiceData data)
    {
        if (data == null)
        {
            return null;
        }

        /* Recordings are not synthesised - they are already on disk. */
        if (data.usesRecording())
        {
            return existingRecording(data);
        }

        if (data.text.get() == null || data.text.get().trim().isEmpty())
        {
            return null;
        }

        VoiceRequest request = data.toRequest(BBSVoiceSettings.sampleRate());
        String signature = request.signature();

        Link ready = READY.get(signature);

        if (ready != null)
        {
            return ready;
        }

        File file = fileFor(signature);

        if (file.isFile() && file.length() > 44)
        {
            Link link = linkFor(signature);

            READY.put(signature, link);

            return link;
        }

        if (FAILED.contains(signature))
        {
            return null;
        }

        if (BBSVoiceSettings.autoRender() && PENDING.add(signature))
        {
            submit(signature, request, file);
        }

        return null;
    }

    public static Status status(VoiceData data)
    {
        if (data == null)
        {
            return Status.EMPTY;
        }

        if (data.usesRecording())
        {
            if (data.recordingLink() == null)
            {
                return Status.EMPTY;
            }

            return existingRecording(data) != null ? Status.READY : Status.FAILED;
        }

        if (data.text.get() == null || data.text.get().trim().isEmpty())
        {
            return Status.EMPTY;
        }

        String signature = resolveSignature(data);

        if (READY.containsKey(signature))
        {
            return Status.READY;
        }

        if (fileFor(signature).isFile())
        {
            return Status.READY;
        }

        if (FAILED.contains(signature))
        {
            return Status.FAILED;
        }

        return PENDING.contains(signature) ? Status.RENDERING : Status.EMPTY;
    }

    /** Drops the cached render of this line and queues it again (the manual "re-synthesise" button). */
    public static void retry(VoiceData data)
    {
        if (data == null || data.usesRecording())
        {
            return;
        }

        if (data.text.get() == null || data.text.get().trim().isEmpty())
        {
            return;
        }

        VoiceRequest request = data.toRequest(BBSVoiceSettings.sampleRate());
        String signature = request.signature();

        FAILED.remove(signature);
        READY.remove(signature);

        File file = fileFor(signature);

        if (file.isFile())
        {
            file.delete();
        }

        /* Forcefully re-render, even when automatic rendering is switched off. */
        if (PENDING.add(signature))
        {
            submit(signature, request, file);
        }
    }

    public static File folder()
    {
        return BBSMod.getAssetsPath(FOLDER);
    }

    private static File fileFor(String signature)
    {
        return new File(folder(), signature + ".wav");
    }

    /** @return the link of a recording that is actually on disk, or {@code null}. */
    private static Link existingRecording(VoiceData data)
    {
        Link link = data.recordingLink();

        if (link == null)
        {
            return null;
        }

        try
        {
            File file = BBSMod.getProvider().getFile(link);

            return file != null && file.isFile() && file.length() > 44 ? link : null;
        }
        catch (Exception e)
        {
            return null;
        }
    }

    private static Link linkFor(String signature)
    {
        return Link.assets(FOLDER + "/" + signature + ".wav");
    }

    private static void submit(String signature, VoiceRequest request, File file)
    {
        POOL.submit(() ->
        {
            try
            {
                short[] pcm = VoiceSynthesis.render(request);

                if (pcm.length == 0)
                {
                    FAILED.add(signature);

                    return;
                }

                Files.createDirectories(file.getParentFile().toPath());
                WavFile.write(file, pcm, request.sampleRate);
                prune();

                MinecraftClient client = MinecraftClient.getInstance();

                if (client != null)
                {
                    client.execute(() -> READY.put(signature, linkFor(signature)));
                }
                else
                {
                    READY.put(signature, linkFor(signature));
                }
            }
            catch (Throwable throwable)
            {
                FAILED.add(signature);
                System.err.println("[BBS Voice] Failed to synthesise speech:");
                throwable.printStackTrace();
            }
            finally
            {
                PENDING.remove(signature);
            }
        });
    }

    /** Keeps the cache folder at or below the configured file count. */
    private static void prune()
    {
        File[] files = folder().listFiles((dir, name) -> name.endsWith(".wav"));

        if (files == null || files.length <= BBSVoiceSettings.maxCacheFiles())
        {
            return;
        }

        List<File> sorted = new ArrayList<>();

        for (File file : files)
        {
            sorted.add(file);
        }

        sorted.sort((a, b) -> Long.compare(a.lastModified(), b.lastModified()));

        int remove = sorted.size() - BBSVoiceSettings.maxCacheFiles();

        for (int i = 0; i < remove; i++)
        {
            sorted.get(i).delete();
        }
    }
}
