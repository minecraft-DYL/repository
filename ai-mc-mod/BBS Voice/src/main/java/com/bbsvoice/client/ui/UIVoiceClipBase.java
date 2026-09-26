package com.bbsvoice.client.ui;

import com.bbsvoice.client.voice.VoiceAudioCache;
import com.bbsvoice.voice.IVoiceClip;
import com.bbsvoice.voice.VoiceData;
import com.bbsvoice.voice.VoicePreset;
import mchorse.bbs_mod.l10n.keys.IKey;
import mchorse.bbs_mod.resources.Link;
import mchorse.bbs_mod.ui.film.IUIClipsDelegate;
import mchorse.bbs_mod.ui.film.audio.UIAudioRecorder;
import mchorse.bbs_mod.ui.film.clips.UIClip;
import mchorse.bbs_mod.ui.framework.UIContext;
import mchorse.bbs_mod.ui.framework.elements.buttons.UIButton;
import mchorse.bbs_mod.ui.framework.elements.buttons.UICirculate;
import mchorse.bbs_mod.ui.framework.elements.buttons.UIIcon;
import mchorse.bbs_mod.ui.framework.elements.buttons.UIToggle;
import mchorse.bbs_mod.ui.framework.elements.input.UITrackpad;
import mchorse.bbs_mod.ui.framework.elements.input.text.UITextbox;
import mchorse.bbs_mod.ui.framework.elements.overlay.UIOverlay;
import mchorse.bbs_mod.ui.framework.elements.overlay.UISoundOverlayPanel;
import mchorse.bbs_mod.ui.framework.elements.overlay.UITextareaOverlayPanel;
import mchorse.bbs_mod.ui.framework.elements.utils.UILabel;
import mchorse.bbs_mod.ui.utils.UI;
import mchorse.bbs_mod.ui.utils.UIUtils;
import mchorse.bbs_mod.ui.utils.icons.Icons;
import mchorse.bbs_mod.utils.clips.Clip;

import java.util.function.Consumer;

/**
 * Shared clip editor for both voice clip flavours (film level camera clip and per actor action
 * clip). Everything the user can tweak lives here: dubbing text, voice character, the dubbing
 * source (offline synthesis or the user's own recording), gain, pitch, pace, offset, fit-to-clip
 * and the volume/speed automation curves.
 */
public abstract class UIVoiceClipBase <T extends Clip & IVoiceClip> extends UIClip<T>
{
    public UITextbox text;
    public UIButton editText;
    public UIButton rerender;
    public UICirculate voiceCycle;
    public UIToggle tone;

    public UICirculate sourceCycle;
    public UIIcon record;
    public UIIcon pickRecording;
    public UILabel recordingName;

    public UITrackpad pitch;
    public UITrackpad volume;
    public UITrackpad speed;
    public UITrackpad offset;
    public UIToggle autoFit;

    public UITrackpad f0;
    public UITrackpad formant;
    public UITrackpad breath;
    public UITrackpad vibrato;
    public UITrackpad jitter;
    public UITrackpad tilt;
    public UICirculate excCycle;

    public UIToggle volumeCurve;
    public UIToggle speedCurve;

    public UILabel status;
    public UIIcon openFolder;

    /** Set while {@link #fillData()} pushes values into the widgets so their callbacks stay quiet. */
    private boolean updating;
    private int frame;

    public UIVoiceClipBase(T clip, IUIClipsDelegate editor)
    {
        super(clip, editor);
    }

    public VoiceData voice()
    {
        return this.clip.getVoiceData();
    }

    @Override
    protected void registerUI()
    {
        super.registerUI();

        this.text = new UITextbox(4000, (value) -> this.editor.editMultiple(this.voice().text, (property) -> property.set(value)));
        this.text.delayedInput().tooltip(VoiceUIKeys.TEXT_TOOLTIP);

        this.editText = new UIButton(VoiceUIKeys.EDIT_TEXT, (b) -> this.openTextEditor());
        this.rerender = new UIButton(VoiceUIKeys.RERENDER, (b) -> this.rerender());

        this.voiceCycle = new UICirculate((cycle) ->
        {
            if (!this.updating)
            {
                this.applyPreset(VoicePreset.all()[cycle.getValue()]);
            }
        });

        for (VoicePreset preset : VoicePreset.all())
        {
            this.voiceCycle.addLabel(VoiceUIKeys.preset(preset.id));
        }

        this.tone = new UIToggle(VoiceUIKeys.TONE, (toggle) -> this.editor.editMultiple(this.voice().tone, (property) -> property.set(toggle.getValue())));
        this.tone.tooltip(VoiceUIKeys.TONE_TOOLTIP);

        /* --- dubbing source --- */

        this.sourceCycle = new UICirculate((cycle) ->
        {
            if (!this.updating)
            {
                String source = cycle.getValue() == 1 ? VoiceData.SOURCE_REC : VoiceData.SOURCE_TTS;

                this.editor.editMultiple(this.voice().source, (property) -> property.set(source));
                this.updateStatus();
            }
        });
        this.sourceCycle.addLabel(VoiceUIKeys.SOURCE_TTS);
        this.sourceCycle.addLabel(VoiceUIKeys.SOURCE_REC);

        this.record = new UIIcon(Icons.MICROPHONE, (b) -> this.startRecording());
        this.record.tooltip(VoiceUIKeys.RECORD_HINT);
        this.pickRecording = new UIIcon(Icons.SOUND, (b) -> this.pickRecording());
        this.pickRecording.tooltip(VoiceUIKeys.PICK_RECORDING);
        this.recordingName = new UILabel(VoiceUIKeys.NO_RECORDING);

        /* --- mix --- */

        this.pitch = this.trackpad(VoiceUIKeys.PITCH, 0.25D, 4D, (value) -> this.editor.editMultiple(this.voice().pitch, (property) -> property.set(value.floatValue())));
        this.volume = this.trackpad(VoiceUIKeys.VOLUME, 0D, 4D, (value) -> this.editor.editMultiple(this.voice().volume, (property) -> property.set(value.floatValue())));
        this.speed = this.trackpad(VoiceUIKeys.SPEED, 0.25D, 4D, (value) -> this.editor.editMultiple(this.voice().speed, (property) -> property.set(value.floatValue())));
        this.offset = this.trackpad(VoiceUIKeys.OFFSET, -600D, 600D, (value) -> this.editor.editMultiple(this.voice().offset, (property) -> property.set(value.floatValue())));
        this.offset.tooltip(VoiceUIKeys.OFFSET_TOOLTIP);

        this.autoFit = new UIToggle(VoiceUIKeys.AUTO_FIT, (toggle) -> this.editor.editMultiple(this.voice().autoFit, (property) -> property.set(toggle.getValue())));
        this.autoFit.tooltip(VoiceUIKeys.AUTO_FIT_TOOLTIP);

        /* --- voice character details --- */

        this.f0 = this.trackpad(VoiceUIKeys.F0, 40D, 600D, (value) -> this.editor.editMultiple(this.voice().f0, (property) -> property.set(value.floatValue())));
        this.formant = this.trackpad(VoiceUIKeys.FORMANT, 0.5D, 1.6D, (value) -> this.editor.editMultiple(this.voice().formant, (property) -> property.set(value.floatValue())));
        this.breath = this.trackpad(VoiceUIKeys.BREATH, 0D, 1D, (value) -> this.editor.editMultiple(this.voice().breath, (property) -> property.set(value.floatValue())));
        this.vibrato = this.trackpad(VoiceUIKeys.VIBRATO, 0D, 0.09D, (value) -> this.editor.editMultiple(this.voice().vibrato, (property) -> property.set(value.floatValue())));
        this.jitter = this.trackpad(VoiceUIKeys.JITTER, 0D, 0.05D, (value) -> this.editor.editMultiple(this.voice().jitter, (property) -> property.set(value.floatValue())));
        this.tilt = this.trackpad(VoiceUIKeys.TILT, 0D, 1D, (value) -> this.editor.editMultiple(this.voice().tilt, (property) -> property.set(value.floatValue())));

        this.excCycle = new UICirculate((cycle) ->
        {
            if (!this.updating)
            {
                this.editor.editMultiple(this.voice().excitation, (property) -> property.set(cycle.getValue()));
            }
        });
        this.excCycle.addLabel(VoiceUIKeys.SOURCE_PULSE);
        this.excCycle.addLabel(VoiceUIKeys.SOURCE_SQUARE);
        this.excCycle.addLabel(VoiceUIKeys.SOURCE_NOISE);

        /* --- curves: enabling one creates its track, which the BBS timeline then edits --- */

        this.volumeCurve = new UIToggle(VoiceUIKeys.VOLUME_CURVE, (toggle) ->
        {
            VoiceData data = this.voice();

            this.editor.editMultiple(data.volumeCurve, (value) -> data.setVolumeCurve(toggle.getValue()));
        });
        this.volumeCurve.tooltip(VoiceUIKeys.CURVE_TOOLTIP);

        this.speedCurve = new UIToggle(VoiceUIKeys.SPEED_CURVE, (toggle) ->
        {
            VoiceData data = this.voice();

            this.editor.editMultiple(data.speedCurve, (value) -> data.setSpeedCurve(toggle.getValue()));
        });
        this.speedCurve.tooltip(VoiceUIKeys.CURVE_TOOLTIP);

        this.status = new UILabel(VoiceUIKeys.STATUS_EMPTY);
        this.openFolder = new UIIcon(Icons.FOLDER, (b) -> UIUtils.openFolder(VoiceAudioCache.folder()));
        this.openFolder.tooltip(VoiceUIKeys.OPEN_FOLDER);
    }

    private UITrackpad trackpad(IKey label, double min, double max, Consumer<Double> callback)
    {
        UITrackpad trackpad = new UITrackpad(callback);

        trackpad.limit(min, max).increment(0.01D).forcedLabel(label);

        return trackpad;
    }

    @Override
    protected void registerPanels()
    {
        super.registerPanels();

        this.panels.add(UI.column(UI.label(VoiceUIKeys.TEXT), this.text, UI.row(this.editText, this.rerender)).marginTop(12));
        this.panels.add(UI.column(UI.label(VoiceUIKeys.CHARACTER), this.voiceCycle, this.tone).marginTop(12));
        this.panels.add(UI.column(UI.label(VoiceUIKeys.DUBBING_SOURCE), this.sourceCycle, UI.row(this.record, this.pickRecording, this.recordingName)).marginTop(12));
        this.panels.add(UI.column(UI.label(VoiceUIKeys.MIX), UI.row(this.pitch, this.volume), UI.row(this.speed, this.offset), this.autoFit).marginTop(12));
        this.panels.add(UI.column(UI.label(VoiceUIKeys.ADVANCED), UI.row(this.f0, this.formant), UI.row(this.breath, this.vibrato), UI.row(this.jitter, this.tilt), this.excCycle).marginTop(12));
        this.panels.add(UI.column(UI.label(VoiceUIKeys.CURVES), this.volumeCurve, this.speedCurve).marginTop(12));
        this.panels.add(UI.column(UI.label(VoiceUIKeys.STATUS), UI.row(this.status, this.openFolder)).marginTop(12));
    }

    @Override
    public void fillData()
    {
        super.fillData();

        VoiceData data = this.voice();

        this.updating = true;

        this.text.setText(data.text.get());
        this.tone.setValue(data.tone.get());
        this.pitch.setValue(data.pitch.get());
        this.volume.setValue(data.volume.get());
        this.speed.setValue(data.speed.get());
        this.offset.setValue(data.offset.get());
        this.autoFit.setValue(data.autoFit.get());
        this.f0.setValue(data.f0.get());
        this.formant.setValue(data.formant.get());
        this.breath.setValue(data.breath.get());
        this.vibrato.setValue(data.vibrato.get());
        this.jitter.setValue(data.jitter.get());
        this.tilt.setValue(data.tilt.get());
        this.volumeCurve.setValue(data.volumeCurveOn());
        this.speedCurve.setValue(data.speedCurveOn());

        this.voiceCycle.setValue(presetIndex(data.preset()));
        this.excCycle.setValue(Math.max(0, Math.min(2, data.excitation.get())));
        this.sourceCycle.setValue(data.usesRecording() ? 1 : 0);

        this.updating = false;

        this.updateStatus();
    }

    @Override
    public void render(UIContext context)
    {
        super.render(context);

        if ((this.frame++ % 10) == 0)
        {
            this.updateStatus();
        }
    }

    private void updateStatus()
    {
        VoiceData data = this.voice();
        Link recording = data.recordingLink();

        this.recordingName.label = recording == null ? VoiceUIKeys.NO_RECORDING : IKey.constant(fileName(recording));

        IKey key;

        switch (VoiceAudioCache.status(data))
        {
            case RENDERING: key = VoiceUIKeys.STATUS_RENDERING; break;
            case READY: key = VoiceUIKeys.STATUS_READY; break;
            case FAILED: key = data.usesRecording() ? VoiceUIKeys.STATUS_MISSING_REC : VoiceUIKeys.STATUS_FAILED; break;
            default: key = data.usesRecording() ? VoiceUIKeys.STATUS_NO_REC : VoiceUIKeys.STATUS_EMPTY; break;
        }

        this.status.label = key;
    }

    private static int presetIndex(VoicePreset preset)
    {
        VoicePreset[] all = VoicePreset.all();

        for (int i = 0; i < all.length; i++)
        {
            if (all[i] == preset)
            {
                return i;
            }
        }

        return 0;
    }

    private static String fileName(Link link)
    {
        String path = link.toString();
        int index = path.lastIndexOf('/');

        return index < 0 ? path : path.substring(index + 1);
    }

    private void openTextEditor()
    {
        VoiceData data = this.voice();
        UITextareaOverlayPanel panel = new UITextareaOverlayPanel(VoiceUIKeys.TEXT_TITLE, VoiceUIKeys.TEXT_MESSAGE, (value) ->
        {
            this.editor.editMultiple(data.text, (property) -> property.set(value));
            this.text.setText(value);

            if (VoiceAudioCache.status(data) != VoiceAudioCache.Status.RENDERING)
            {
                VoiceAudioCache.retry(data);
            }
        });

        panel.text.setText(data.text.get());
        UIOverlay.addOverlay(this.getContext(), panel, 0.9F, 0.7F);
    }

    /** Opens BBS' own microphone recorder and turns the result into this clip's dubbing. */
    private void startRecording()
    {
        UIAudioRecorder.start(this.getContext(), "bbsvoice_" + System.currentTimeMillis(), (name, wave) ->
        {
            try
            {
                UIAudioRecorder.saveWave(name, wave);
            }
            catch (Exception e)
            {
                System.err.println("[BBS Voice] Could not save the recording:");
                e.printStackTrace();
            }

            Link link = UIAudioRecorder.toAudioLink(name);
            VoiceData data = this.voice();

            this.editor.editMultiple(data.recording, (property) -> property.set(link));
            this.editor.editMultiple(data.source, (property) -> property.set(VoiceData.SOURCE_REC));

            this.fillData();
        });
    }

    /** Lets the user reuse any audio file that is already in BBS' audio folder. */
    private void pickRecording()
    {
        VoiceData data = this.voice();
        UISoundOverlayPanel panel = new UISoundOverlayPanel((link) ->
        {
            this.editor.editMultiple(data.recording, (property) -> property.set(link));
            this.editor.editMultiple(data.source, (property) -> property.set(VoiceData.SOURCE_REC));

            this.fillData();
        }, this.getContext());

        UIOverlay.addOverlay(this.getContext(), panel, 0.9F, 0.6F);
    }

    private void applyPreset(VoicePreset preset)
    {
        VoiceData data = this.voice();

        this.editor.editMultiple(data.voice, (property) -> property.set(preset.id));
        this.editor.editMultiple(data.f0, (property) -> property.set(preset.f0));
        this.editor.editMultiple(data.formant, (property) -> property.set(preset.formant));
        this.editor.editMultiple(data.breath, (property) -> property.set(preset.breath));
        this.editor.editMultiple(data.vibrato, (property) -> property.set(preset.vibrato));
        this.editor.editMultiple(data.jitter, (property) -> property.set(preset.jitter));
        this.editor.editMultiple(data.tilt, (property) -> property.set(preset.tilt));
        this.editor.editMultiple(data.excitation, (property) -> property.set(preset.excitation));

        this.fillData();
    }

    private void rerender()
    {
        VoiceAudioCache.retry(this.voice());
        this.updateStatus();
    }
}
