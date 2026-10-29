using TagLib;

namespace MediaVault.Services
{
    public class MusicTagService
    {
        public MusicTags ReadTags(string filePath)
        {
            try
            {
                using var tfile = TagLib.File.Create(filePath);

                return new MusicTags
                {
                    Title = tfile.Tag.Title,
                    Artist = tfile.Tag.FirstPerformer ?? tfile.Tag.FirstAlbumArtist,
                    Album = tfile.Tag.Album,
                    Year = (int)tfile.Tag.Year,
                    Genre = tfile.Tag.FirstGenre
                };
            }
            catch
            {
                return null;
            }
        }
    }

    public class MusicTags
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public int? Year { get; set; }
        public string Genre { get; set; }
    }
}