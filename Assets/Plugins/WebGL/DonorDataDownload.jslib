mergeInto(LibraryManager.library, {
  DownloadDonorDataFile: function(fileNamePtr, contentPtr, mimeTypePtr) {
    var fileName = UTF8ToString(fileNamePtr);
    var content = UTF8ToString(contentPtr);
    var mimeType = UTF8ToString(mimeTypePtr);

    var blob = new Blob([content], { type: mimeType });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    window.setTimeout(function() {
      URL.revokeObjectURL(url);
    }, 0);
  }
});
