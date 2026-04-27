mergeInto(LibraryManager.library, {
  RequestDonorDataUpload: function(targetObjectNamePtr, targetMethodNamePtr) {
    var targetObjectName = UTF8ToString(targetObjectNamePtr);
    var targetMethodName = UTF8ToString(targetMethodNamePtr);

    var input = document.createElement("input");
    input.type = "file";
    input.accept = ".json,application/json";
    input.style.display = "none";

    input.onchange = function(event) {
      var file = event.target.files && event.target.files[0];
      if (!file) {
        document.body.removeChild(input);
        return;
      }

      var reader = new FileReader();
      reader.onload = function(loadEvent) {
        SendMessage(targetObjectName, targetMethodName, loadEvent.target.result || "");
        document.body.removeChild(input);
      };
      reader.onerror = function() {
        SendMessage(targetObjectName, targetMethodName, "");
        document.body.removeChild(input);
      };
      reader.readAsText(file);
    };

    document.body.appendChild(input);
    input.click();
  }
});
