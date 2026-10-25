using Android.Content;
using Android.Provider;
using AzurChat.Model;

namespace AzurChat.Platforms.Android;

public static class AndroidPhotoService
{
    public static List<PhotoItem> GetPhotos()
    {
        var photos = new List<PhotoItem>();

        var resolver = Platform.AppContext.ContentResolver;

        if (resolver == null)
            return photos;

        var collection = MediaStore.Images.Media.ExternalContentUri;

        if (collection == null)
            return photos;

        string[] projection =
        {
            MediaStore.Images.Media.InterfaceConsts.Id
        };

        using var cursor = resolver.Query(
            collection,
            projection,
            null,
            null,
            $"{MediaStore.Images.Media.InterfaceConsts.DateAdded} DESC");

        if (cursor == null)
            return photos;

        int idColumn = cursor.GetColumnIndexOrThrow(
            MediaStore.Images.Media.InterfaceConsts.Id);

        while (cursor.MoveToNext())
        {
            long id = cursor.GetLong(idColumn);

            var uri = ContentUris.WithAppendedId(collection, id);

            if (uri == null)
                continue;

            string uriString = uri.ToString() ?? "";

            photos.Add(new PhotoItem
            {
                Uri = uriString,
                Thumbnail = ImageSource.FromStream(() =>
                {
                    var imageUri = global::Android.Net.Uri.Parse(uriString);

                    return resolver.OpenInputStream(imageUri)
                           ?? throw new IOException("无法读取照片");
                })
            });
        }

        return photos;
    }
}