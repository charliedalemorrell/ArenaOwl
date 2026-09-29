// The streaming services you can pick from. Edit this list to add or change services.
// A new service's domain also needs adding to the content_scripts matches in manifest.json,
// or fullscreen on that site will take over the whole monitor.
export const PLATFORMS = [
  { key: "prime",     name: "Prime Video",      url: "https://www.amazon.com/gp/video/storefront" },
  { key: "espn",      name: "ESPN",             url: "https://www.espn.com/watch/" },
  { key: "foxone",    name: "Fox One",          url: "https://www.foxone.com/" },
  { key: "paramount", name: "Paramount+ (CBS)", url: "https://www.paramountplus.com/" },
  { key: "netflix",   name: "Netflix",          url: "https://www.netflix.com/browse" },
  { key: "peacock",   name: "Peacock",          url: "https://www.peacocktv.com/" },
  { key: "youtubetv", name: "YouTube TV",       url: "https://tv.youtube.com/" },
  { key: "disney",    name: "Disney+",          url: "https://www.disneyplus.com/" },
  { key: "hulu",      name: "Hulu",             url: "https://www.hulu.com/" },
  { key: "tubi",      name: "Tubi",             url: "https://tubitv.com/" },
  { key: "hbomax",    name: "HBO Max",          url: "https://www.hbomax.com/" },
];
