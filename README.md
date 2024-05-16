# Open MIDI Visualizer

Open MIDI Visualizer, or OMIDIV for short, or OMV for shorter, or Open Musical Information Data Interchange Visualizer for long, is an open source MIDI visualizer and audio player.

Not a midi *player*. Just a visualizer.  
If you want audio, render it separately and open it.  
**If you change something and it doesn't do anything, try pressing F6 (to reload the midi, can be done while playing), or F5 (to reload and reset everything).** I'm working on ways to do this automatically without being laggy.  
If the audio doesn't sync up with the notes, use the Offset (ms) in the audio or midi control window.  

Demo videos:  
[Open MIDI Visualizer (Alpha Demo)](https://youtu.be/4YJwQmvFq10)  
[Unwavering Sea](https://youtube.com/playlist?list=PLXOldc20MYD6b-hVZ-qRbORx8SYlpIOom&si=il2OAE9FXeKJlGKp)  
[Monsterpiece](https://youtu.be/hWmX9x6rStI?si=_1VDDwxDIkh-i75G)  
[Tempura Ocha's videos](https://youtu.be/oIgni18ZyaE?si=SIXOOQlJNNdrIZ1e)  
(some videos above may have lots of post-processing video effects)

**No license?**  
I'll add a proper open-source license once the first release version (v1.x.x, not alpha or beta) is released. Until then:  

- You are free to modify a version of it for your own use, but if you choose to distribute that modified version, make it clear that its a modified version of this.

Also, its not a requirement but, if you like this visualizer and use it to make some content, like a youtube video, credit would be appreciated.

The older engineless version of OMIDIV is available [here](https://github.com/TheGoldenProof/OMIDIV-CPP). There isn't much reason to use it, but it might handle insane note counts slightly better.

## What's Next / To Do

- Try to get mac builds to work
- Update notifier using GitHub API
- Midis using SMPTE time divisions probably dont work (I don't even know where to get a midi that uses SMPTE time, so until someone runs into issues with that, this is going on the back burner).
- Other visualizations -- I'm open to suggestions, I'd love to hear your ideas and requests.
- Add opened audio file to video recording.
