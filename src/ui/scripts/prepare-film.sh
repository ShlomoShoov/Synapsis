#!/usr/bin/env bash
#
# prepare-film.sh: turns a source video into a Synapsis ambient film, ready for assets/media/.
#
# What it does, in order:
#   1. Rotates the loop: the film starts at --start seconds and wraps around to the source's beginning, with a
#      cross-dissolve where the source's end meets its start. The last frame of the file then leads straight into
#      its first frame, so the loop has no visible seam.
#   2. Grades it to ink and paper: the source's darkest tones become the page's ink, its brightest tones become the
#      page's paper (the two colours in src/styles/tokens.css). A bright sky then reads as the page itself.
#   3. Encodes two files, so every browser gets the best one it can play:
#        <name>.av1.mp4    AV1, 10-bit (small, smooth gradients; modern browsers)
#        <name>.h264.mp4   H.264, 8-bit (plays everywhere)
#   4. Writes the poster, the first frame of the film as AVIF: shown while the film loads, and instead of the film
#      when the visitor asks for reduced motion.
#
# Usage:
#   scripts/prepare-film.sh <source.mp4> <output-folder> <name> [--start SECONDS] [--fade SECONDS]
#                           [--black LEVEL] [--white LEVEL] [--width PIXELS]
#
# Example (the home page film):
#   scripts/prepare-film.sh ~/Downloads/pexels-35782324.mp4 assets/media/home winter-flock --start 24
#
# Needs ffmpeg 6 or later with libsvtav1, libx264 and libaom (Ubuntu's ffmpeg package has all three).
# --black and --white are the source levels (0-255) that become pure ink and pure paper: measure them on a frame
# (the darkest shadow and the brightest sky) and set them so the sky lands exactly on paper.

set -euo pipefail

# The page's colours, from src/styles.css (--paper and --ink). Change them together.
PAPER_R=244; PAPER_G=240; PAPER_B=231
INK_R=29;    INK_G=26;    INK_B=21

usage() {
  sed -n '3,26p' "$0" | sed 's/^# \{0,1\}//'
  exit 1
}

if [[ $# -lt 3 ]]; then
  usage
fi

SOURCE="$1"
OUTPUT="$2"
NAME="$3"
shift 3

START=0
FADE=2
BLACK=10
WHITE=242
WIDTH=2560

while [[ $# -gt 0 ]]; do
  case "$1" in
    --start) START="$2"; shift 2 ;;
    --fade)  FADE="$2"; shift 2 ;;
    --black) BLACK="$2"; shift 2 ;;
    --white) WHITE="$2"; shift 2 ;;
    --width) WIDTH="$2"; shift 2 ;;
    *) echo "Unknown option: $1" >&2; usage ;;
  esac
done

if [[ ! -f "$SOURCE" ]]; then
  echo "Source not found: $SOURCE" >&2
  exit 1
fi
mkdir -p "$OUTPUT"

DURATION=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$SOURCE")

# One channel of the grade: a straight line from (BLACK -> ink) to (WHITE -> paper), held flat outside it.
level() {
  local ink="$1" paper="$2"
  echo "clip(${ink}+(val-${BLACK})*(${paper}-${ink})/(${WHITE}-${BLACK}),${ink},${paper})"
}
GRADE="lutrgb=r='$(level $INK_R $PAPER_R)':g='$(level $INK_G $PAPER_G)':b='$(level $INK_B $PAPER_B)'"

# The loop: part A runs from START to the end of the source, part B from the beginning to START. B dissolves in
# over the last FADE seconds of A. When START is 0 the source is used as it is.
if awk "BEGIN { exit !($START > 0) }"; then
  OFFSET=$(awk "BEGIN { printf \"%.3f\", ($DURATION - $START) - $FADE }")
  LOOP="[0:v]split[s1][s2];"
  LOOP+="[s1]trim=start=${START},setpts=PTS-STARTPTS[a];"
  LOOP+="[s2]trim=end=${START},setpts=PTS-STARTPTS[b];"
  LOOP+="[a][b]xfade=transition=fade:duration=${FADE}:offset=${OFFSET}[looped];"
else
  LOOP="[0:v]null[looped];"
fi

FILTER="${LOOP}[looped]scale=${WIDTH}:-2:flags=lanczos,${GRADE}"
COLOUR=(-colorspace bt709 -color_primaries bt709 -color_trc bt709 -color_range tv)

echo "Grading and encoding AV1 (slow, a few minutes)..."
ffmpeg -v error -stats -y -i "$SOURCE" -an \
  -filter_complex "${FILTER},format=yuv420p10le" \
  -c:v libsvtav1 -preset 4 -crf 30 -g 150 -svtav1-params tune=0 \
  "${COLOUR[@]}" -movflags +faststart "$OUTPUT/$NAME.av1.mp4"

echo "Grading and encoding H.264..."
ffmpeg -v error -stats -y -i "$SOURCE" -an \
  -filter_complex "${FILTER},format=yuv420p" \
  -c:v libx264 -preset slow -crf 21 -profile:v high -tune film -g 60 \
  "${COLOUR[@]}" -movflags +faststart "$OUTPUT/$NAME.h264.mp4"

echo "Writing the poster..."
ffmpeg -v error -y -i "$OUTPUT/$NAME.h264.mp4" -frames:v 1 \
  -c:v libaom-av1 -still-picture 1 -crf 26 -pix_fmt yuv420p10le "$OUTPUT/$NAME.poster.avif"

echo
echo "Done. Files and the codec strings for src/media/films.ts:"
for file in "$OUTPUT/$NAME.av1.mp4" "$OUTPUT/$NAME.h264.mp4" "$OUTPUT/$NAME.poster.avif"; do
  printf '  %-48s %8s KB\n' "$file" "$(( $(stat -c %s "$file") / 1024 ))"
done
ffprobe -v error -select_streams v:0 -show_entries stream=codec_name,profile,level,width,height,pix_fmt \
  -of compact "$OUTPUT/$NAME.av1.mp4"
ffprobe -v error -select_streams v:0 -show_entries stream=codec_name,profile,level,width,height,pix_fmt \
  -of compact "$OUTPUT/$NAME.h264.mp4"
