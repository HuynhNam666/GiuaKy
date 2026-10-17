(function ($) {
  var $form = $("#UploadForm"),
    $file = $("#ExamFile"),
    svc = abp.services.app;

  var MAX = 20 * 1024 * 1024;

  function sizeText(n) {
    return n >= 1048576 ? (n / 1048576).toFixed(1) + " MB" : Math.max(1, Math.round(n / 1024)) + " KB";
  }

  // Chọn / kéo thả file -> chuyển sang trạng thái "File đã chọn" (Figma W2).
  function showChosen(file) {
    $("#FileName").text(file.name);
    $("#FileSize").text(sizeText(file.size));
    $("#DropIdle").addClass("tz-hidden");
    $("#FileChosen").removeClass("tz-hidden");
    $("#BtnUpload").prop("disabled", false);
  }

  function accept(file) {
    if (!file) return false;
    if (!/\.docx$/i.test(file.name)) { abp.message.warn("Chỉ chấp nhận file .docx (Word)."); return false; }
    if (file.size > MAX) { abp.message.warn("File vượt quá 20 MB."); return false; }
    return true;
  }

  $("#BtnPick, #BtnPickAgain").on("click", function () { $file.trigger("click"); });
  $file.on("change", function () {
    var f = this.files[0];
    if (accept(f)) showChosen(f);
    else { this.value = ""; }
  });

  var $drop = $("#DropIdle");
  $drop.on("dragover dragenter", function (e) { e.preventDefault(); $drop.addClass("drag"); });
  $drop.on("dragleave drop", function (e) { e.preventDefault(); $drop.removeClass("drag"); });
  $drop.on("drop", function (e) {
    var files = e.originalEvent.dataTransfer && e.originalEvent.dataTransfer.files;
    if (files && files.length && accept(files[0])) {
      var dt = new DataTransfer();
      dt.items.add(files[0]);
      $file[0].files = dt.files;
      showChosen(files[0]);
    }
  });

  $form.on("submit", function (e) {
    var f = $file[0].files[0];
    if (!f) {
      e.preventDefault();
      abp.message.warn("Hãy chọn một file .docx.");
      return;
    }
    $form.find("button[type=submit]").prop("disabled", true).text("Đang kiểm tra...");
  });

  // ----- vòng đời phiên bản -----
  function ids($el) { return { versionId: parseInt($el.data("version-id"), 10) }; }

  $(document).on("click", ".js-publish", function () {
    var input = ids($(this));
    abp.message.confirm("Sinh viên sẽ thấy đề và được bắt đầu thi. Phiên bản đã xuất bản không thể sửa; muốn sửa phải tạo bản sửa.", "Xuất bản đề thi?", function (ok) {
      if (!ok) return;
      svc.examPublish.publish(input).done(function (r) {
        if (r.success) { abp.notify.success("Đã xuất bản."); location.reload(); }
        else {
          var more = r.errors.length > 8 ? " … (còn " + (r.errors.length - 8) + " lỗi, mở Xem trước để xem đủ)" : "";
          abp.message.error(r.errors.slice(0, 8).map(function (e) { return "• " + e.message; }).join(" ") + more, "Chưa thể xuất bản — đề còn lỗi");
        }
      });
    });
  });
  $(document).on("click", ".js-unpublish", function () {
    var input = ids($(this));
    abp.message.confirm("Sinh viên sẽ không thể bắt đầu bài thi mới (các lượt đang làm vẫn hoàn thành).", "Dừng nhận thi?", function (ok) {
      if (ok) svc.examPublish.unpublish(input).done(function () { location.reload(); });
    });
  });
  $(document).on("click", ".js-draft", function () {
    svc.examPublish.createDraft(ids($(this))).done(function (id) { location.href = abp.appPath + "Exams/Preview/" + id; });
  });

  var $errors = $("#ImportErrors");
  if ($errors.length) {
    $errors[0].scrollIntoView({ behavior: "smooth", block: "start" });
  }
})(jQuery);
