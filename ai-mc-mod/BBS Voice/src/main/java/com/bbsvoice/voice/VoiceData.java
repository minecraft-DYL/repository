package com.bbsvoice.voice;

import com.bbsvoice.tts.VoiceRequest;
import mchorse.bbs_mod.resources.Link;
import mchorse.bbs_mod.settings.values.base.BaseKeyframeFactoryValue;
import mchorse.bbs_mod.settings.values.core.ValueGroup;
import mchorse.bbs_mod.settings.values.core.ValueLink;
import mchorse.bbs_mod.settings.values.core.ValueString;
import mchorse.bbs_mod.settings.values.numeric.ValueBoolean;
import mchorse.bbs_mod.settings.values.numeric.ValueFloat;
import mchorse.bbs_mod.settings.values.numeric.ValueInt;
import mchorse.bbs_mod.utils.keyframes.KeyframeChannel;
import mchorse.bbs_mod.utils.keyframes.factories.KeyframeFactories;

/**
 * Everything that makes up one voice-over: the line of text, the voice character, and the
 * two automation curves (volume + speed) that behave like the ones in a video editor.
 *
 * <p>All of the fields are BBS {@code BaseValue}s so they are saved with the film and are
 * undoable through BBS' own undo system. The curves are ordinary {@link KeyframeChannel}s,
 * which is exactly what BBS' keyframe graph editor consumes.</p>
 */
public class VoiceData extends ValueGroup
{
    /** Dubbing source: synthesise the text offline. */
    public static final String SOURCE_TTS = "tts";

    /** Dubbing source: use a recording of the user's own voice. */
    public static final String SOURCE_REC = "rec";

    /* --- content --- */

    public final ValueString text = new ValueString("text", "");
    public final ValueString voice = new ValueString("voice", VoicePreset.MALE.id);

    /* --- dubbing source --- */

    public final ValueString source = new ValueString("source", SOURCE_TTS);
    public final ValueLink recording = new ValueLink("recording", null);

    /* --- voice character --- */

    public final ValueFloat f0 = new ValueFloat("f0", 120F, 40F, 600F);
    public final ValueFloat formant = new ValueFloat("formant", 1F, 0.5F, 1.6F);
    public final ValueFloat breath = new ValueFloat("breath", 0.08F, 0F, 1F);
    public final ValueFloat vibrato = new ValueFloat("vibrato", 0.012F, 0F, 0.09F);
    public final ValueFloat jitter = new ValueFloat("jitter", 0.006F, 0F, 0.05F);
    public final ValueFloat tilt = new ValueFloat("tilt", 0.45F, 0F, 1F);
    public final ValueInt excitation = new ValueInt("excitation", 0, 0, 2);

    /* --- utterance level controls (applied live during playback) --- */

    public final ValueBoolean tone = new ValueBoolean("tone", true);
    public final ValueFloat pitch = new ValueFloat("pitch", 1F, 0.25F, 4F);
    public final ValueFloat volume = new ValueFloat("volume", 1F, 0F, 4F);
    public final ValueFloat speed = new ValueFloat("speed", 1F, 0.25F, 4F);
    public final ValueFloat offset = new ValueFloat("offset", 0F, -600F, 600F);

    /**
     * When the clip is shorter than the utterance, speed the utterance up so that it still fits
     * inside the clip instead of being cut off half way through (video editor "fit to duration").
     */
    public final ValueBoolean autoFit = new ValueBoolean("auto_fit", false);

    /* --- automation curves --- */

    /**
     * The curves are real BBS animatable values ({@code BaseKeyframeFactoryValue}) rather than bare
     * {@link KeyframeChannel}s, because that is what BBS 2.2 discovers when it builds the track
     * sheets of the timeline. Creating the track is what "enabling" the curve means.
     */
    public final BaseKeyframeFactoryValue<Double> volumeCurve = new BaseKeyframeFactoryValue<>("volume_curve", KeyframeFactories.DOUBLE, 1D);
    public final BaseKeyframeFactoryValue<Double> speedCurve = new BaseKeyframeFactoryValue<>("speed_curve", KeyframeFactories.DOUBLE, 1D);

    public VoiceData(String id)
    {
        super(id);

        this.add(this.text);
        this.add(this.voice);
        this.add(this.source);
        this.add(this.recording);

        this.add(this.f0);
        this.add(this.formant);
        this.add(this.breath);
        this.add(this.vibrato);
        this.add(this.jitter);
        this.add(this.tilt);
        this.add(this.excitation);

        this.add(this.tone);
        this.add(this.pitch);
        this.add(this.volume);
        this.add(this.speed);
        this.add(this.offset);
        this.add(this.autoFit);

        this.add(this.volumeCurve);
        this.add(this.speedCurve);
    }

    public VoicePreset preset()
    {
        return VoicePreset.byId(this.voice.get());
    }

    /** @return true when this clip should play a recording instead of synthesising speech. */
    public boolean usesRecording()
    {
        return SOURCE_REC.equals(this.source.get());
    }

    public Link recordingLink()
    {
        return this.recording.get();
    }

    /**
     * Copies every parameter of a voice preset into this clip. Presets are just starting points —
     * the clip stores its own copy of the values, so tweaking one clip never affects another.
     */
    public void applyPreset(VoicePreset preset)
    {
        this.voice.set(preset.id);
        this.f0.set(preset.f0);
        this.formant.set(preset.formant);
        this.breath.set(preset.breath);
        this.vibrato.set(preset.vibrato);
        this.jitter.set(preset.jitter);
        this.tilt.set(preset.tilt);
        this.excitation.set(preset.excitation);
    }

    /**
     * Build the (pure Java, Minecraft free) synthesis request for this clip.
     */
    public VoiceRequest toRequest(int sampleRate)
    {
        VoiceRequest request = new VoiceRequest();

        request.text = this.text.get();
        request.sampleRate = sampleRate;
        request.f0 = this.f0.get();
        request.formant = this.formant.get();
        request.breath = this.breath.get();
        request.vibrato = this.vibrato.get();
        request.jitter = this.jitter.get();
        request.tilt = this.tilt.get();
        request.excitation = this.excitation.get();
        request.tone = this.tone.get();

        return request;
    }

    /**
     * Volume multiplier at a given tick inside the clip (0 = clip start). This is the final
     * multiplier: the plain volume of the clip times the automation curve.
     */
    public float volumeAt(float tick)
    {
        return Math.max(0F, this.volume.get() * this.volumeCurve.evaluateAt(tick).floatValue());
    }

    /**
     * Speed multiplier at a given tick inside the clip (0 = clip start). This is the final
     * multiplier: the plain pace of the clip times the automation curve.
     */
    public float speedAt(float tick)
    {
        return Math.max(0.05F, this.speed.get() * this.speedCurve.evaluateAt(tick).floatValue());
    }

    public boolean volumeCurveOn()
    {
        return this.volumeCurve.hasTrack();
    }

    public boolean speedCurveOn()
    {
        return this.speedCurve.hasTrack();
    }

    /** Enabling a curve means creating its track, and that is what the timeline edits. */
    public void setVolumeCurve(boolean enabled)
    {
        if (enabled && !this.volumeCurve.hasTrack())
        {
            this.volumeCurve.createTrack().insert(0F, 1D);
        }
        else if (!enabled && this.volumeCurve.hasTrack())
        {
            this.volumeCurve.removeTrack();
        }
    }

    public void setSpeedCurve(boolean enabled)
    {
        if (enabled && !this.speedCurve.hasTrack())
        {
            this.speedCurve.createTrack().insert(0F, 1D);
        }
        else if (!enabled && this.speedCurve.hasTrack())
        {
            this.speedCurve.removeTrack();
        }
    }
}
