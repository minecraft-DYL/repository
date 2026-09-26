package com.bbsvoice.voice;

/**
 * The bundled offline voice characters ("配音人物"). Each preset is a set of source-filter
 * parameters for the formant synthesiser, they do not depend on any external TTS service.
 */
public class VoicePreset
{
    public final String id;
    public final String labelKey;
    public final String labelFallback;

    /** Base fundamental frequency in Hz. */
    public final float f0;

    /** Vocal tract scale: multiplies every formant frequency. Less than 1 = shorter tract (female/child). */
    public final float formant;

    /** Breathiness: amount of aspiration noise mixed into the voiced source (0..1). */
    public final float breath;

    /** Vibrato depth as a fraction of f0 (e.g. 0.012 = +/- 1.2%). */
    public final float vibrato;

    /** Random period-to-period f0 perturbation (0..0.05). */
    public final float jitter;

    /** Spectral tilt: 0 = dark/muffled, 1 = bright. */
    public final float tilt;

    /**
     * Excitation waveform.
     * 0 = natural glottal pulse, 1 = square (retro/robot), 2 = pure noise (whisper).
     */
    public final int excitation;

    public VoicePreset(String id, String labelFallback, float f0, float formant, float breath, float vibrato, float jitter, float tilt, int excitation)
    {
        this.id = id;
        this.labelKey = "bbsvoice.voice." + id;
        this.labelFallback = labelFallback;
        this.f0 = f0;
        this.formant = formant;
        this.breath = breath;
        this.vibrato = vibrato;
        this.jitter = jitter;
        this.tilt = tilt;
        this.excitation = excitation;
    }

    public static final VoicePreset MALE_DEEP = new VoicePreset("male_deep", "男声 · 低沉", 88F, 1.10F, 0.05F, 0.010F, 0.006F, 0.30F, 0);
    public static final VoicePreset MALE = new VoicePreset("male", "男声 · 标准", 112F, 1.00F, 0.06F, 0.012F, 0.006F, 0.45F, 0);
    public static final VoicePreset MALE_BRIGHT = new VoicePreset("male_bright", "男声 · 清亮", 132F, 0.97F, 0.08F, 0.014F, 0.005F, 0.60F, 0);
    public static final VoicePreset FEMALE = new VoicePreset("female", "女声 · 标准", 196F, 0.94F, 0.07F, 0.014F, 0.005F, 0.55F, 0);
    public static final VoicePreset FEMALE_SOFT = new VoicePreset("female_soft", "女声 · 温柔", 178F, 0.95F, 0.16F, 0.020F, 0.006F, 0.42F, 0);
    public static final VoicePreset FEMALE_BRIGHT = new VoicePreset("female_bright", "女声 · 活泼", 232F, 0.92F, 0.05F, 0.016F, 0.004F, 0.68F, 0);
    public static final VoicePreset CHILD = new VoicePreset("child", "童声", 286F, 0.84F, 0.06F, 0.015F, 0.006F, 0.62F, 0);
    public static final VoicePreset ELDER = new VoicePreset("elder", "老人 · 沧桑", 104F, 1.06F, 0.18F, 0.012F, 0.030F, 0.35F, 0);
    public static final VoicePreset ROBOT = new VoicePreset("robot", "机器人", 118F, 1.00F, 0.00F, 0.000F, 0.000F, 0.75F, 1);
    public static final VoicePreset WHISPER = new VoicePreset("whisper", "耳语", 140F, 1.00F, 1.00F, 0.006F, 0.015F, 0.50F, 2);
    public static final VoicePreset CUSTOM = new VoicePreset("custom", "自定义", 120F, 1.00F, 0.08F, 0.012F, 0.006F, 0.50F, 0);

    public static final VoicePreset[] PRESETS = new VoicePreset[] {
        MALE_DEEP, MALE, MALE_BRIGHT, FEMALE, FEMALE_SOFT, FEMALE_BRIGHT, CHILD, ELDER, ROBOT, WHISPER, CUSTOM
    };

    public static VoicePreset byId(String id)
    {
        if (id != null)
        {
            for (VoicePreset preset : PRESETS)
            {
                if (preset.id.equals(id))
                {
                    return preset;
                }
            }
        }

        return MALE;
    }

    public static VoicePreset[] all()
    {
        return PRESETS;
    }
}
