package com.bbsvoice.tts;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Phone inventory of the built in synthesiser, together with the articulatory targets that drive
 * the source-filter model. Values are standard Mandarin/English formant measurements (male-ish
 * vocal tract); the voice presets rescale them.
 *
 * <p>A phoneme is a short sequence of {@link Part}s so that stop consonants ("b", "p", "t"…) can
 * be modelled as closure + burst (+ aspiration) instead of a single static frame.</p>
 */
public class Phoneme
{
    public enum Kind
    {
        VOWEL,
        GLIDE,
        NASAL,
        LIQUID,
        FRICATIVE,
        STOP,
        AFFRICATE,
        SILENCE
    }

    public static class Part
    {
        public final float duration;
        public final float f1;
        public final float f2;
        public final float f3;
        public final float voiced;
        public final float noise;

        public Part(float duration, float f1, float f2, float f3, float voiced, float noise)
        {
            this.duration = duration;
            this.f1 = f1;
            this.f2 = f2;
            this.f3 = f3;
            this.voiced = voiced;
            this.noise = noise;
        }
    }

    private static final Map<String, Phoneme> REGISTRY = new LinkedHashMap<>();

    public final String id;
    public final Kind kind;
    public final boolean nucleus;
    public final Part[] parts;

    private Phoneme(String id, Kind kind, boolean nucleus, Part[] parts)
    {
        this.id = id;
        this.kind = kind;
        this.nucleus = nucleus;
        this.parts = parts;
    }

    public static Phoneme get(String id)
    {
        return REGISTRY.get(id);
    }

    public static Map<String, Phoneme> registry()
    {
        return REGISTRY;
    }

    private static Phoneme add(Phoneme phoneme)
    {
        REGISTRY.put(phoneme.id, phoneme);

        return phoneme;
    }

    private static Part part(float duration, float f1, float f2, float f3, float voiced, float noise)
    {
        return new Part(duration, f1, f2, f3, voiced, noise);
    }

    private static Phoneme vowel(String id, float f1, float f2, float f3, float duration)
    {
        return add(new Phoneme(id, Kind.VOWEL, true, new Part[] {part(duration, f1, f2, f3, 1F, 0.02F)}));
    }

    private static Phoneme glide(String id, float f1, float f2, float f3)
    {
        return add(new Phoneme(id, Kind.GLIDE, false, new Part[] {part(58F, f1, f2, f3, 0.9F, 0.02F)}));
    }

    private static Phoneme nasal(String id, float f1, float f2, float f3, float duration)
    {
        return add(new Phoneme(id, Kind.NASAL, false, new Part[] {part(duration, f1, f2, f3, 0.6F, 0.05F)}));
    }

    private static Phoneme liquid(String id, float f1, float f2, float f3, float voiced, float noise)
    {
        return add(new Phoneme(id, Kind.LIQUID, false, new Part[] {part(68F, f1, f2, f3, voiced, noise)}));
    }

    private static Phoneme fricative(String id, float f1, float f2, float f3, float voiced, float noise, float duration)
    {
        return add(new Phoneme(id, Kind.FRICATIVE, false, new Part[] {part(duration, f1, f2, f3, voiced, noise)}));
    }

    private static Phoneme stop(String id, float f1, float f2, float f3, boolean aspirated)
    {
        Part closure = part(32F, 250F, f2 * 0.6F, f3, 0F, 0F);
        Part burst = part(12F, f1, f2, f3, 0.06F, 1F);

        if (aspirated)
        {
            Part aspiration = part(64F, f1 * 0.9F, f2 * 0.95F, f3, 0.10F, 0.55F);

            return add(new Phoneme(id, Kind.STOP, false, new Part[] {closure, burst, aspiration}));
        }

        return add(new Phoneme(id, Kind.STOP, false, new Part[] {closure, burst}));
    }

    private static Phoneme affricate(String id, float f1, float f2, float f3, boolean aspirated)
    {
        Part closure = part(26F, 250F, f2 * 0.6F, f3, 0F, 0F);
        Part frication = part(aspirated ? 78F : 62F, f1, f2, f3, 0.08F, 0.92F);

        return add(new Phoneme(id, Kind.AFFRICATE, false, new Part[] {closure, frication}));
    }

    /* --- vowels (Mandarin) --- */
    public static final Phoneme A = vowel("a", 850F, 1250F, 2800F, 145F);
    public static final Phoneme O = vowel("o", 520F, 900F, 2600F, 138F);
    public static final Phoneme E = vowel("e", 500F, 1250F, 2400F, 130F);
    public static final Phoneme EH = vowel("eh", 600F, 1900F, 2600F, 132F);
    public static final Phoneme I = vowel("i", 300F, 2300F, 3000F, 128F);
    public static final Phoneme U = vowel("u", 350F, 800F, 2400F, 134F);
    public static final Phoneme V = vowel("v", 300F, 1900F, 2400F, 128F);
    public static final Phoneme ER = vowel("er", 500F, 1400F, 1700F, 150F);
    public static final Phoneme APICAL = vowel("apical", 350F, 1300F, 2200F, 130F);
    public static final Phoneme DENTAL = vowel("dental", 350F, 1600F, 2500F, 128F);
    public static final Phoneme SCHWA = vowel("schwa", 500F, 1500F, 2500F, 95F);

    /* --- English-ish vowels --- */
    public static final Phoneme AE = vowel("ae", 700F, 1700F, 2600F, 140F);
    public static final Phoneme AH = vowel("ah", 700F, 1200F, 2600F, 135F);
    public static final Phoneme AO = vowel("ao", 570F, 850F, 2400F, 145F);
    public static final Phoneme AA = vowel("aa", 750F, 1050F, 2700F, 145F);
    public static final Phoneme IH = vowel("ih", 400F, 1900F, 2600F, 105F);

    /* --- glides --- */
    public static final Phoneme Y = glide("y", 300F, 2300F, 3000F);
    public static final Phoneme W = glide("w", 350F, 800F, 2400F);

    /* --- nasals --- */
    public static final Phoneme M = nasal("m", 250F, 1100F, 2200F, 72F);
    public static final Phoneme N = nasal("n", 250F, 1700F, 2600F, 68F);
    public static final Phoneme NG = nasal("ng", 250F, 1500F, 2500F, 84F);

    /* --- liquids / approximants --- */
    public static final Phoneme L = liquid("l", 350F, 1100F, 2800F, 0.85F, 0.03F);
    public static final Phoneme R = liquid("r", 350F, 1250F, 1750F, 0.80F, 0.06F);
    public static final Phoneme RR = liquid("rr", 400F, 1300F, 1700F, 0.75F, 0.10F);

    /* --- fricatives --- */
    public static final Phoneme F = fricative("f", 1300F, 3500F, 5200F, 0F, 0.75F, 92F);
    public static final Phoneme VF = fricative("vf", 1300F, 3500F, 5200F, 0.35F, 0.35F, 78F);
    public static final Phoneme S = fricative("s", 350F, 3800F, 6800F, 0F, 0.95F, 104F);
    public static final Phoneme Z = fricative("z", 350F, 3800F, 6800F, 0.35F, 0.45F, 80F);
    public static final Phoneme SH = fricative("sh", 350F, 1700F, 2500F, 0F, 0.90F, 106F);
    public static final Phoneme X = fricative("x", 300F, 2600F, 3600F, 0F, 0.85F, 104F);
    public static final Phoneme H = fricative("h", 600F, 1300F, 2300F, 0F, 0.65F, 88F);
    public static final Phoneme TH = fricative("th", 300F, 1600F, 3000F, 0F, 0.70F, 84F);
    public static final Phoneme DH = fricative("dh", 300F, 1600F, 3000F, 0.30F, 0.35F, 70F);

    /* --- stops / affricates --- */
    public static final Phoneme B = stop("b", 400F, 800F, 2200F, false);
    public static final Phoneme P = stop("p", 400F, 800F, 2200F, true);
    public static final Phoneme D = stop("d", 400F, 1700F, 2600F, false);
    public static final Phoneme T = stop("t", 400F, 1700F, 2600F, true);
    public static final Phoneme G = stop("g", 400F, 2000F, 2600F, false);
    public static final Phoneme K = stop("k", 400F, 2000F, 2600F, true);
    public static final Phoneme J = affricate("j", 300F, 2200F, 3000F, false);
    public static final Phoneme Q = affricate("q", 300F, 2200F, 3000F, true);
    public static final Phoneme ZH = affricate("zh", 350F, 1400F, 2300F, false);
    public static final Phoneme CH = affricate("ch", 350F, 1400F, 2300F, true);
    public static final Phoneme ZD = affricate("zd", 350F, 1700F, 2600F, false);
    public static final Phoneme CD = affricate("cd", 350F, 1700F, 2600F, true);

    /* --- silence / pause --- */
    public static final Phoneme SILENCE = add(new Phoneme("sil", Kind.SILENCE, false, new Part[] {part(120F, 500F, 1500F, 2500F, 0F, 0F)}));
}
