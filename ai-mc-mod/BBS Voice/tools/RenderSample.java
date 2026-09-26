import com.bbsvoice.tts.*;
import com.bbsvoice.voice.VoicePreset;

import java.io.File;
import java.util.List;

/**
 * Dev tool: renders a few sample lines with the offline synthesiser so the engine can be listened to
 * without launching Minecraft.
 *
 * <p>Usage: {@code java -cp <classes>:src/main/resources tools/RenderSample.java <output folder>}</p>
 */
public class RenderSample
{
    public static void main(String[] args) throws Exception
    {
        File folder = new File(args.length > 0 ? args[0] : "build/samples");

        folder.mkdirs();

        String[][] lines = {
            {"zh_hello", "你好，世界！这是一段离线合成的配音测试。", "male"},
            {"zh_story", "从前有座山，山里有座庙。", "female"},
            {"zh_curve", "音量曲线和语速曲线都可以调整。", "child"},
            {"en_hello", "Hello world, this is an offline voice test.", "male_deep"},
            {"zh_robot", "机器人配音模式已启动。", "robot"},
        };

        for (String[] line : lines)
        {
            VoiceRequest request = new VoiceRequest();

            request.text = line[1];
            request.sampleRate = 22050;

            VoicePreset preset = VoicePreset.byId(line[2]);

            request.f0 = preset.f0;
            request.formant = preset.formant;
            request.breath = preset.breath;
            request.vibrato = preset.vibrato;
            request.jitter = preset.jitter;
            request.tilt = preset.tilt;
            request.excitation = preset.excitation;

            List<Phone> phones = Phonemizer.phonemize(request.text, request.tone);

            System.out.println("=== " + line[0] + " (" + phones.size() + " phones) ===");
            System.out.println("  " + phones);

            long start = System.nanoTime();
            short[] pcm = VoiceSynthesis.render(request);
            long elapsed = (System.nanoTime() - start) / 1000000L;

            File file = new File(folder, line[0] + ".wav");

            WavFile.write(file, pcm, request.sampleRate);

            System.out.println("  -> " + file.getName() + "  " + pcm.length + " samples, "
                + String.format("%.2f", VoiceSynthesis.durationSeconds(pcm, request.sampleRate)) + "s, "
                + elapsed + "ms, signature " + request.signature());
        }
    }
}
