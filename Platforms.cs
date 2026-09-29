namespace ArenaOwl;

enum BrowserKind { Edge, Chrome }

record Platform(string Name, string ProfileKey, string Url)
{
    public override string ToString() => Name;

    // Edit this list to add or change services.
    public static readonly Platform[] All =
    {
        new("Prime Video",      "Prime",     "https://www.amazon.com/gp/video/storefront"),
        new("ESPN",             "ESPN",      "https://www.espn.com/watch/"),
        new("Fox One",          "FoxOne",    "https://www.foxone.com/"),
        new("Paramount+ (CBS)", "Paramount", "https://www.paramountplus.com/"),
        new("Netflix",          "Netflix",   "https://www.netflix.com/browse"),
        new("Peacock",          "Peacock",   "https://www.peacocktv.com/"),
        new("YouTube TV",       "YouTubeTV", "https://tv.youtube.com/"),
    };
}
