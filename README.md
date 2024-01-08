# Open MIDI Visualizer

Open MIDI Visualizer, or OMIDIV for short, or OMV for shorter, or Open Musical Information Data Interchange Visualizer for long, is an open source MIDI visualizer and audio player.

Not a midi *player*. Just a visualizer.  
If you want audio, render it separately and open it.  
**If you change something and it doesn't do anything, try pressing F6 (to reload the midi, can be done while playing), or F5 (to reload and reset everything).** I'm working on ways to do this automatically without being laggy.  
If the audio doesn't sync up with the notes, use the Offset (ms) in the audio or midi control window.  

Demo videos:  
[Open MIDI Visualizer (Alpha Demo)](https://youtu.be/4YJwQmvFq10)

**No license?**  
I'll add a proper open-source license once the first release version (v1.x.x, not alpha or beta) is released. Until then:  
- You are free to modify a version of it for your own use, but don't distribute that version.  
- If you make public any content made with it (like youtube video), including with your modified version, give credit.

The older engineless version of OMIDIV is available [here](https://github.com/TheGoldenProof/OMIDIV-CPP). There isn't much reason to use it, but it might handle insane note counts slightly better.

## What's Next / To Do
- ffmpeg export
- other visualizations
- Midis using SMPTE time divisions probably dont work
- Update notifier using GitHub API
