import com.bbsvoice.tts.Phone;
import com.bbsvoice.tts.Phonemizer;
import com.bbsvoice.tts.VoiceRequest;
import com.bbsvoice.tts.VoiceSynthesis;
import com.bbsvoice.tts.WavFile;
import com.bbsvoice.voice.VoicePreset;

import java.io.File;
import java.util.List;

/**
 * Dev tool: renders a single sustained vowel (and a few variants) so the source itself can be
 * measured - a lone "a" must show a clean 120 Hz harmonic series under a 850/1250/2800 Hz formant
 * envelope. If instead the low end dominates or the harmonics are not at multiples of f0, the
 * problem is in the synthesiser, not in the game or in the mixer.
 *
 * usage: java -cp <classes>:src/main/resources tools/RenderVowel.java <output folder>
 */
public class RenderVowel
{
    public static void main(String[] args) throws Exception
    {
        File folder = new File(args.length > 0 ? args[0] : "build/vowels");

        folder.mkdirs();

        render(folder, "a_120", "啊", "male", 120F, 1F, 0);
        render(folder, "a_120_square", "啊", "male", 120F, 1F, 1);
        render(folder, "a_120_noise", "啊", "male", 120F, 1F, 2);
        render(folder, "a_200_female", "啊", "female", 200F, 1F, 0);
        render(folder, "i_120", "衣", "male", 120F, 1F, 0);
        render(folder, "i_120_noise", "衣", "male", 120F, 1F, 2);
        render(folder, "ni_hao", "你好", "male", 120F, 1F, 0);
        render(folder, "sibilant", "四十四只石狮子", "male", 120F, 1F, 0);
    }

    private static void render(File folder, String name, String text, String presetId, float f0, float formant, int excitation) throws Exception
    {
        VoiceRequest request = new VoiceRequest();
        VoicePreset preset = VoicePreset.byId(presetId);

        request.text = text;
        request.sampleRate = 22050;
        request.f0 = f0;
        request.formant = formant;
        request.breath = preset.breath;
        request.vibrato = preset.vibrato;
        request.jitter = preset.jitter;
        request.tilt = preset.tilt;
        request.excitation = excitation;

        List<Phone> phones = Phonemizer.phonemize(request.text, request.tone);
        short[] pcm = VoiceSynthesis.render(request);
        File file = new File(folder, name + ".wav");

        WavFile.write(file, pcm, request.sampleRate);

        System.out.println("=== " + name + "  f0=" + f0 + " formant=" + formant + " excitation=" + excitation
            + "  phones=" + phones.size() + "  " + String.format("%.3f", pcm.length / (float) request.sampleRate) + "s");
        System.out.println("    " + phones);
    }
}
