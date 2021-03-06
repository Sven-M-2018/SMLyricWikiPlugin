﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using ImPluginEngine.Abstractions;
using ImPluginEngine.Abstractions.Entities;
using ImPluginEngine.Abstractions.Interfaces;
using ImPluginEngine.Helpers;
using ImPluginEngine.Types;

namespace SMLyricWikiPlugin
{
    public class SMLyricWikiPlugin : IPlugin, ILyrics
    {
        public string Name => "SMLyricWiki";
        public string Version => "1.0.0";

        public async Task GetLyrics(PluginLyricsInput input, CancellationToken ct, Action<PluginLyricsResult> updateAction)
        {
            String url = string.Format("http://lyrics.wikia.com/api.php?action=lyrics&artist={0}&song={1}&fmt=xml", HttpUtility.UrlEncode(input.Artist), HttpUtility.UrlEncode(input.Title));
            var client = new HttpClient();
            String xml = string.Empty;
            try
            {
                var response = await client.GetAsync(url, ct);
                var data = await response.Content.ReadAsByteArrayAsync();
                xml = Encoding.UTF8.GetString(data);
            }
            catch (HttpRequestException)
            {
                return;
            }
            XDocument xdoc = XDocument.Parse(xml);
            if (xdoc.Element("LyricsResult").Element("lyrics").Value.ToString() != "Not found")
            {
                if (xdoc.Element("LyricsResult").Element("lyrics").Value.ToString() == "Instrumental")
                {
                    var result = new PluginLyricsResult();
                    result.Artist = xdoc.Element("LyricsResult").Element("artist").Value.ToString();
                    result.Title = xdoc.Element("LyricsResult").Element("song").Value.ToString();
                    result.FoundByPlugin = string.Format("{0} v{1}", Name, Version);
                    result.Lyrics = "<p>[Instrumental]</p>\n<p><i><sub>powered by LyricWiki</sub></i></p>";
                    updateAction(result);
                }
                else
                {
                    url = xdoc.Element("LyricsResult").Element("url").Value.ToString();
                    String lyrics = await DownloadLyrics(url, ct);
                    if (!string.IsNullOrEmpty(lyrics))
                    {
                        var result = new PluginLyricsResult();
                        result.Artist = xdoc.Element("LyricsResult").Element("artist").Value.ToString();
                        result.Title = xdoc.Element("LyricsResult").Element("song").Value.ToString();
                        result.FoundByPlugin = string.Format("{0} v{1}", Name, Version);
                        result.Lyrics = lyrics;
                        updateAction(result);
                    }
                    lyrics = await DownloadLyrics("http://lyrics.wikia.com/Gracenote:" + url.Substring(24), ct);
                    if (!string.IsNullOrEmpty(lyrics))
                    {
                        var result = new PluginLyricsResult();
                        result.Artist = xdoc.Element("LyricsResult").Element("artist").Value.ToString();
                        result.Title = string.Format("Gracenote: {0}", xdoc.Element("LyricsResult").Element("song").Value.ToString());
                        result.FoundByPlugin = string.Format("{0} v{1}", Name, Version);
                        result.Lyrics = lyrics;
                        updateAction(result);
                    }
                }
            }
        }

        private async Task<String> DownloadLyrics(String url, CancellationToken ct)
        {
            var client = new HttpClient();
            string web;
            try
            {
                var response = await client.GetAsync(url, ct);
                var data = await response.Content.ReadAsByteArrayAsync();
                web = Encoding.UTF8.GetString(data);
            }
            catch (HttpRequestException)
            {
                return string.Empty;
            }
            Regex LyricsRegex = new Regex(@"<div class='lyricbox'>(?'Lyrics'.+)<div class='lyricsbreak'>", RegexOptions.Compiled);
            string lyrics = string.Empty;
            var match = LyricsRegex.Match(web);
            if (match.Success)
            {
                if (match.Groups["Lyrics"].Value != "&#10;" && !WebUtility.HtmlDecode(match.Groups["Lyrics"].Value).Contains("Unfortunately, we are not licensed to display the full lyrics"))
                {
                    lyrics = CleanLyrics(WebUtility.HtmlDecode(match.Groups["Lyrics"].Value));
                }
            }
            return lyrics;
        }

        private static string CleanLyrics(String lyrics)
        {
            lyrics = Regex.Replace(lyrics, "<a href=[^>]+>", "", RegexOptions.IgnoreCase);
            lyrics = lyrics.Replace("</a>", "");
            lyrics = lyrics.Replace("<br />", "\n").Replace("<br/>", "\n").Replace("<br>", "\n").Replace("\n", "<br/>\n");
            lyrics = lyrics.Replace("<br/>\n<br/>\n", "</p>\n<p>");
            lyrics = lyrics.Replace("´", "'").Replace("`", "'").Replace("’", "'").Replace("‘", "'");
            lyrics = lyrics.Replace("…", "...").Replace(" ...", "...");
            lyrics = lyrics.Replace("<p><br/>\n", "<p>\n");
            lyrics = Regex.Replace(lyrics, @"\s+<br/>", "<br/>", RegexOptions.IgnoreCase);
            lyrics = Regex.Replace(lyrics, @"\s+<p/>", "<p/>", RegexOptions.IgnoreCase);
            return "<p>" + lyrics.Trim() + "</p>\n<p><i><sub>powered by LyricWiki</sub></i></p>";
        }
    }
}
