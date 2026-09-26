package com.bbsvoice.client;

import com.bbsvoice.BBSVoice;
import com.bbsvoice.client.ui.VoiceUIKeys;
import com.bbsvoice.client.voice.AudioSupport;
import com.bbsvoice.client.voice.VoiceActionClientClip;
import com.bbsvoice.client.voice.VoiceAudioCache;
import com.bbsvoice.client.voice.VoiceClientClip;
import com.bbsvoice.voice.VoiceClip;
import com.bbsvoice.voice.VoiceData;
import mchorse.bbs_mod.BBSMod;
import mchorse.bbs_mod.BBSModClient;
import mchorse.bbs_mod.audio.SoundBuffer;
import mchorse.bbs_mod.audio.SoundPlayer;
import mchorse.bbs_mod.data.types.BaseType;
import mchorse.bbs_mod.resources.Link;
import mchorse.bbs_mod.ui.film.IUIClipsDelegate;
import mchorse.bbs_mod.ui.film.clips.UIClip;
import mchorse.bbs_mod.utils.clips.Clip;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents;
import net.minecraft.client.MinecraftClient;

import java.io.File;
import java.io.InputStream;
import java.io.PrintWriter;
import java.lang.reflect.Proxy;
import java.util.ArrayList;
import java.util.List;

/**
 * Headless in game smoke test, activated with {@code -Dbbsvoice.selftest=1}.
 *
 * <p>It boots inside the real client (inside the real user installation) and exercises everything
 * that can only break at runtime: the clip factory registration, the data serialisation round trip,
 * the language files, the internal source pack, the offline renderer + sound cache + OpenAL load,
 * the recording branch and the editor panel construction. Then it writes a report and quits.</p>
 *
 * <p>This is a development tool, it does nothing unless the system property is set.</p>
 */
public final class BBSVoiceSelfTest
{
    private static final String PREFIX = "[bbsvoice-selftest]";

    private static final List<String> RESULTS = new ArrayList<>();

    private static int ticks;
    private static int waitStart = -1;
    private static int loadStart = -1;
    private static int quitAt = -1;
    private static boolean started;
    private static boolean loaded;
    private static boolean noted;
    private static VoiceClip clip;
    private static VoiceData data;
    private static Link rendered;

    private BBSVoiceSelfTest()
    {}

    public static void register()
    {
        if (System.getProperty("bbsvoice.selftest") == null)
        {
            return;
        }

        System.out.println(PREFIX + " enabled, waiting for the client to settle");

        ClientTickEvents.END_CLIENT_TICK.register(BBSVoiceSelfTest::tick);
    }

    private static void tick(MinecraftClient client)
    {
        /* The title screen is enough, none of the checks need a loaded world. */
        ticks++;

        if (quitAt > 0)
        {
            if (ticks >= quitAt)
            {
                client.scheduleStop();
            }

            return;
        }

        if (!started)
        {
            if (ticks < 40)
            {
                return;
            }

            started = true;

            try
            {
                run();
            }
            catch (Throwable t)
            {
                fail("selftest", t);
                finish(client);
            }

            return;
        }

        if (rendered == null)
        {
            waitForRender(client);
        }
        else if (!loaded)
        {
            if (!noted)
            {
                noted = true;
                note("audio state", "openal=" + AudioSupport.isReady()
                    + " lwjglLayout=" + AudioSupport.supported()
                    + " mixer=" + (client.getSoundManager() == null ? "null" : client.getSoundManager().getClass().getName()));
            }

            if (!AudioSupport.isReady())
            {
                /* The client may still be pulling up its sound engine; do not touch OpenAL before
                   Minecraft created the AL context, and give up honestly if it never does. */
                if (ticks - loadStart > 1200)
                {
                    skip("sound buffer loaded", "OpenAL unavailable in this session (no audio device?)");
                    finish(client);
                }

                return;
            }

            loaded = true;
            loadSound(client);
            finish(client);
        }
    }

    private static void run() throws Exception
    {
        /* 1. the clip factories, which is how a voice track gets created in the editor */
        Clip camera = BBSMod.getFactoryCameraClips().create(BBSVoice.VOICE_CLIP);
        Clip action = BBSMod.getFactoryActionClips().create(BBSVoice.VOICE_CLIP);

        check("clip factory (film track)", camera instanceof VoiceClientClip, describe(camera));
        check("clip factory (actor track)", action instanceof VoiceActionClientClip, describe(action));

        if (!(camera instanceof VoiceClip))
        {
            throw new IllegalStateException("the film factory did not return a voice clip");
        }

        clip = (VoiceClip) camera;
        data = clip.getVoiceData();

        /* 2. serialisation round trip, this is what saving a film does */
        data.text.set("self test 自检");
        data.f0.set(150F);
        data.autoFit.set(true);
        data.source.set(VoiceData.SOURCE_TTS);

        BaseType saved = clip.toData();
        VoiceClip reloaded = new VoiceClip();

        reloaded.fromData(saved);

        check("data round trip (text)", "self test 自检".equals(reloaded.getVoiceData().text.get()), reloaded.getVoiceData().text.get());
        check("data round trip (numbers)", reloaded.getVoiceData().f0.get() == 150F, String.valueOf(reloaded.getVoiceData().f0.get()));
        check("data round trip (boolean)", reloaded.getVoiceData().autoFit.get(), String.valueOf(reloaded.getVoiceData().autoFit.get()));
        check("data round trip (link is null safe)", reloaded.getVoiceData().recordingLink() == null, String.valueOf(reloaded.getVoiceData().recordingLink()));

        /* 3. language files load after the addon reload */
        String text = VoiceUIKeys.TEXT.get();

        check("l10n loaded", text != null && !text.isEmpty() && !"bbsvoice.ui.text".equals(text), text);

        /* 4. our own source pack is registered */
        try (InputStream stream = BBSMod.getProvider().getAsset(new Link(BBSVoice.MOD_ID, "strings/zh_cn.json")))
        {
            check("source pack registered", stream != null && stream.read() >= 0, "strings/zh_cn.json");
        }

        /* 5. the recording branch must never crash, even without a recording */
        data.source.set(VoiceData.SOURCE_REC);
        VoiceAudioCache.Status missing = VoiceAudioCache.status(data);

        check("recording branch without a file", VoiceAudioCache.get(data) == null && missing != VoiceAudioCache.Status.READY, String.valueOf(missing));
        data.source.set(VoiceData.SOURCE_TTS);

        /* 5b. the automation curves are real animatable values with a discoverable track */
        data.setVolumeCurve(true);
        check("volume curve track created", data.volumeCurveOn() && data.volumeAt(0F) == data.volume.get(), data.volumeAt(0F) + " vs " + data.volume.get());
        data.setVolumeCurve(false);
        check("volume curve track removed", !data.volumeCurveOn(), String.valueOf(data.volumeCurveOn()));

        /* 6. the editor panel, built through the same factory the clips panel uses */
        String panel = buildPanel(clip);

        check("editor panel", panel.startsWith("built"), panel);

        /* 7. kick off the offline renderer */
        VoiceAudioCache.retry(data);
        waitStart = ticks;
    }

    private static String buildPanel(VoiceClip clip)
    {
        try
        {
            IUIClipsDelegate delegate = (IUIClipsDelegate) Proxy.newProxyInstance(
                IUIClipsDelegate.class.getClassLoader(),
                new Class<?>[] { IUIClipsDelegate.class },
                (proxy, method, args) ->
                {
                    Class<?> type = method.getReturnType();

                    if (type == boolean.class) return false;
                    if (type == int.class) return 0;
                    if (type == float.class) return 0F;
                    if (type == double.class) return 0D;
                    if (type == long.class) return 0L;

                    return null;
                });

            UIClip panel = UIClip.createPanel(clip, delegate);

            return panel != null && panel.panels != null ? "built, " + panel.getClass().getSimpleName() : "null panel";
        }
        catch (Throwable t)
        {
            fail("editor panel", t);

            return "threw " + t;
        }
    }

    private static void waitForRender(MinecraftClient client)
    {
        VoiceAudioCache.Status status = VoiceAudioCache.status(data);

        if (status == VoiceAudioCache.Status.READY)
        {
            rendered = VoiceAudioCache.get(data);

            if (rendered != null)
            {
                loadStart = ticks;
                check("offline render", true, String.valueOf(rendered));
            }
        }
        else if (status == VoiceAudioCache.Status.FAILED)
        {
            fail("offline render", new IllegalStateException("renderer reported FAILED"));
            finish(client);
        }
        else if (ticks - waitStart > 1200)
        {
            fail("offline render", new IllegalStateException("timed out after 60s, last status " + status));
            finish(client);
        }
    }

    private static void loadSound(MinecraftClient client)
    {
        if (rendered == null)
        {
            return;
        }

        try
        {
            SoundBuffer buffer = BBSModClient.getSounds().get(rendered, true);
            float duration = buffer == null ? 0F : buffer.getDuration();

            check("sound buffer loaded", buffer != null && duration > 0F, duration + "s");

            SoundPlayer player = BBSModClient.getSounds().playUnique(rendered);
            boolean playing = player != null;

            if (playing)
            {
                player.setVolume(0F);
                player.play();
                player.pause();
                BBSModClient.getSounds().stop(rendered);
            }

            check("openal playback started", playing, playing ? "ok" : "no player");
        }
        catch (Throwable t)
        {
            fail("sound buffer loaded", t);
        }
    }

    private static void check(String name, boolean ok, String detail)
    {
        String line = (ok ? "PASS" : "FAIL") + " | " + name + " | " + detail;

        RESULTS.add(line);
        System.out.println(PREFIX + " " + line);
    }

    private static void fail(String name, Throwable t)
    {
        String line = "FAIL | " + name + " | " + t;

        RESULTS.add(line);
        System.out.println(PREFIX + " " + line);
        t.printStackTrace(System.out);
    }

    /** For checks that cannot run in this environment (reported, never counted as a pass). */
    private static void skip(String name, String reason)
    {
        String line = "SKIP | " + name + " | " + reason;

        RESULTS.add(line);
        System.out.println(PREFIX + " " + line);
    }

    /** Purely informational, ignored by the pass/fail tally. */
    private static void note(String name, String detail)
    {
        String line = "INFO | " + name + " | " + detail;

        RESULTS.add(line);
        System.out.println(PREFIX + " " + line);
    }

    private static String describe(Object object)
    {
        return object == null ? "null" : object.getClass().getName();
    }

    private static void finish(MinecraftClient client)
    {
        int passed = 0;
        int failed = 0;
        int skipped = 0;

        for (String line : RESULTS)
        {
            if (line.startsWith("PASS"))
            {
                passed++;
            }
            else if (line.startsWith("SKIP"))
            {
                skipped++;
            }
            else if (!line.startsWith("INFO"))
            {
                failed++;
            }
        }

        String summary = "SELFTEST " + (failed == 0 ? "PASS" : "FAIL") + " passed=" + passed + " failed=" + failed
            + (skipped > 0 ? " skipped=" + skipped : "");

        RESULTS.add(summary);
        System.out.println(PREFIX + " " + summary);

        try (PrintWriter writer = new PrintWriter(new File(client.runDirectory, "bbsvoice-selftest.txt"), "UTF-8"))
        {
            for (String line : RESULTS)
            {
                writer.println(line);
            }
        }
        catch (Exception e)
        {
            System.out.println(PREFIX + " could not write the report: " + e);
        }

        /* Leave the window up for a moment so the run can be observed, then quit. */
        try
        {
            client.getWindow().setTitle("Minecraft 1.20.4 - BBS Voice selftest " + summary);
        }
        catch (Throwable ignored)
        {}

        quitAt = ticks + 200;
    }
}
