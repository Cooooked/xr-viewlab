using System;
using XRViewLab.UI;

static void Require(bool condition, string message)
{
	if (!condition) throw new InvalidOperationException("FAILED: " + message);
	Console.WriteLine("PASS: " + message);
}

// Same title+artist is treated as the same track, so repeated MediaPropertiesChanged events for
// an unchanged track (pause/seek/volume) do not requeue the card.
Require(MediaSessionEventProvider.TrackKey("Song A", "Artist X") == MediaSessionEventProvider.TrackKey("Song A", "Artist X"),
	"identical title+artist produce the same dedup key");

// A different artist (e.g. a cover, or the same song title from a different track) is a new track.
Require(MediaSessionEventProvider.TrackKey("Song A", "Artist X") != MediaSessionEventProvider.TrackKey("Song A", "Artist Y"),
	"same title with a different artist is a different track");

// A different title is a new track even with the same artist.
Require(MediaSessionEventProvider.TrackKey("Song A", "Artist X") != MediaSessionEventProvider.TrackKey("Song B", "Artist X"),
	"same artist with a different title is a different track");

// Leading/trailing whitespace from provider metadata must not defeat dedup.
Require(MediaSessionEventProvider.TrackKey("  Song A ", "Artist X") == MediaSessionEventProvider.TrackKey("Song A", "Artist X  "),
	"incidental whitespace around title/artist does not create a spurious new key");

// The separator between title and artist must prevent field-concatenation collisions
// (e.g. title="AB", artist="" must not collide with title="A", artist="B").
Require(MediaSessionEventProvider.TrackKey("AB", "") != MediaSessionEventProvider.TrackKey("A", "B"),
	"title/artist concatenation without a real separator would falsely collide; the key must not");

Console.WriteLine("Media session dedup-key fixtures passed.");
