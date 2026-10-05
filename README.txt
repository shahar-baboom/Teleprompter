TELEPROMPTER for Windows 11  (Hebrew / RTL ready)
=================================================

This Teleprompter was created to fill a need for a free Prompter software that has full Hebrew support.

FIRST RUN (one time, about 5 seconds)
  1. Unzip this folder anywhere (e.g. Desktop).
  2. Double-click  build.bat
     It compiles Teleprompter.exe with the C# compiler that is already part of
     Windows 11 (.NET Framework 4.8). Nothing is downloaded or installed.
  3. Teleprompter.exe opens automatically. From now on just run Teleprompter.exe
     (it is a single file - you can copy it to any Windows 10/11 PC).

USING IT
  Script (left)      Type or paste the script. Open/Save .txt files (UTF-8 or Hebrew ANSI).
  Operator preview   Exactly what the prompter monitor shows, but never mirrored.
                     Mouse wheel here (or over the prompter monitor) scrolls the text,
                     in the same direction as the script window.
  Prompter monitor   Choose the monitor, click "Show on monitor". It always opens
                     full-screen and centred on that monitor, whatever its resolution
                     or Windows scaling. Mirror / Flip vertical affect only that monitor.
                     Esc (on that monitor) or "Close prompter" closes it.
  Text               Font, size, line spacing, side margin.
  Reading marker     Show/hide the orange eye-line marker and set its height (10-80%).
  Alignment          Left / Center / Right (absolute on-screen position).
  Direction          RTL (default, Hebrew) or LTR. Mixed Hebrew + English + numbers are
                     laid out with the Unicode bidirectional algorithm.
  Playback           Play / Pause / Stop (Stop returns to the start). Speed slider
                     can be changed while playing. All scrolling is smooth.

  Voice control      Tick "Move only while I speak", pick the microphone and press Play.
                     The text moves at the set speed while you talk and eases to a stop
                     when you pause. Set Threshold so the meter crosses the orange line
                     only when you speak; Hold = how long it keeps moving after you stop.
                     Works with any language (it reacts to your voice, not the words).
                     If the mic won't open: Settings > Privacy & security > Microphone >
                     'Let desktop apps access your microphone' = On.
  Dark mode          Interface > Dark mode.

Settings and the last script are remembered in %APPDATA%\Teleprompter.
