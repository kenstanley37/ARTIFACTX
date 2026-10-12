# Third-party notices

ArtifactX includes work from the following projects.

## nms_namegen

ArtifactX's star system generator (`src/ArtifactX.Core/Universe/`: star colour,
economy, wealth, conflict, dominant race, system flags, planet layout, and
region/system/planet names) is a C# port of **nms_namegen**, and its letter
data (`namegen-data.json.gz`) comes from that project.

- Project: https://github.com/hadsh/nms_namegen
- Authors: Stuart Coyle, had.sh, GoodGuysFree
- Licence: MIT

```
MIT License

Copyright (c) 2026 Stuart Coyle & had.sh & GGF

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

The C# port was checked against nms_namegen's own golden test vectors (443
systems, every field identical) and its 1,000-system hand-recorded ground
truth (same accuracy as the original: star colour 99.1%, race 99.1%,
economy 99.0%, wealth 98.9%, conflict 99.0%, region names 100%).
ArtifactX and nms_namegen are fan projects and are not associated with
Hello Games.
