(function ($) {
  var $root = $("#PreviewRoot"),
    versionId = parseInt($root.data("version-id"), 10),
    svc = abp.services.app;

  function esc(s) {
    return $("<div/>").text(s == null ? "" : String(s)).html();
  }

  function renderIssues(issues) {
    if (!issues || !issues.length) {
      return '<div class="alert alert-success mb-0"><i class="fas fa-check-circle"></i> Hợp lệ: đề đủ 100 câu, mỗi câu 4 lựa chọn, có đáp án và giải thích, chỗ trống Part 6 khớp câu hỏi.</div>';
    }
    var rows = issues
      .map(function (i, idx) {
        return "<tr><td>" + (idx + 1) + "</td><td><code>" + esc(i.code) + "</code></td><td>" + esc(i.message) + "</td></tr>";
      })
      .join("");
    return (
      '<div class="alert alert-danger"><i class="fas fa-exclamation-triangle"></i> ' + issues.length + ' vấn đề cần sửa:</div>' +
      '<table class="table table-sm table-striped"><thead><tr><th>#</th><th>Mã lỗi</th><th>Mô tả</th></tr></thead><tbody>' + rows + "</tbody></table>"
    );
  }

  function showIssues(title, issues) {
    $("#IssuesTitle").text(title);
    $("#IssuesBody").html(renderIssues(issues));
    $("#IssuesModal").modal("show");
  }

  // Sau khi lưu: nếu còn vấn đề thì cho giảng viên xem rồi mới tải lại trang.
  function afterEdit(result, $modal) {
    if ($modal && $modal.hasClass("tz-drawer")) closeDrawer();
    else if ($modal) $modal.modal("hide");
    abp.notify.success("Đã lưu.");
    if (result && result.issues && result.issues.length) {
      $("#IssuesModal").one("hidden.bs.modal hidden", function () { location.reload(); });
      showIssues("Đã lưu, nhưng phiên bản còn vấn đề chưa hợp lệ", result.issues);
    } else {
      location.reload();
    }
  }

  // ----- ngăn kéo sửa câu hỏi (Figma W4) -----
  function openDrawer() {
    $("#DrawerOverlay").addClass("open");
    $("#QuestionDrawer").addClass("open");
  }
  function closeDrawer() {
    $("#DrawerOverlay").removeClass("open");
    $("#QuestionDrawer").removeClass("open");
  }
  $("#DrawerOverlay").on("click", closeDrawer);
  $(document).on("click", "[data-drawer-close]", closeDrawer);
  $(document).on("keydown", function (e) { if (e.key === "Escape") closeDrawer(); });

  // ----- sửa câu hỏi -----
  $(document).on("click", ".js-edit-question", function () {
    var q = $(this).data("question");
    $("#QTitle").text("Sửa câu " + q.no);
    $("#QSub").text((q.group ? q.group + " · " : "") + "id " + q.code);
    $("#QId").val(q.id);
    $("#QStem").val(q.stem);
    $("#QStemHint").text(q.part6 ? "(Part 6: có thể để trống)" : "");
    q.options.forEach(function (o) { $("#QOpt" + o.code).val(o.text); });
    $("input[name=QCorrect][value=" + q.correct + "]").prop("checked", true);
    $("#QExplain").val(q.explanation);
    $("#QShuffle").prop("checked", !!q.shuffleOptions);
    openDrawer();
  });

  $("#QuestionForm").on("submit", function (e) {
    e.preventDefault();
    var options = $(".q-option").map(function () { return { code: $(this).data("code"), text: $(this).val() }; }).get();
    var input = {
      questionId: parseInt($("#QId").val(), 10),
      stem: $("#QStem").val(),
      explanation: $("#QExplain").val(),
      shuffleOptions: $("#QShuffle").is(":checked"),
      correctOptionCode: $("input[name=QCorrect]:checked").val() || "",
      options: options,
    };
    svc.examEdit.updateQuestion(input).done(function (r) { afterEdit(r, $("#QuestionDrawer")); });
  });

  // ----- sửa văn bản bài đọc -----
  $(document).on("click", ".js-edit-block", function () {
    $("#BId").val($(this).data("block-id"));
    $("#BText").val($(this).attr("data-block-text"));
    $("#BlockModal").modal("show");
  });
  $("#BlockForm").on("submit", function (e) {
    e.preventDefault();
    svc.examEdit.updateBlockText({ blockId: parseInt($("#BId").val(), 10), text: $("#BText").val() })
      .done(function (r) { afterEdit(r, $("#BlockModal")); });
  });

  // ----- hoán vị -----
  $(document).on("click", ".js-edit-shuffle", function () {
    var $a = $(this), partNo = $a.data("part-no"), hasGroup = $a.data("group-id") != null;
    $("#SPartId").val($a.data("part-id"));
    $("#SGroupId").val(hasGroup ? $a.data("group-id") : "");
    $("#SPart").prop("checked", String($a.data("part-shuffle")) === "true");
    $("#SPartLabel").text(partNo === 5 ? "Hoán vị các câu độc lập của Part 5" : "Hoán vị nguyên nhóm bài đọc (giữ nguyên thứ tự câu trong nhóm)");
    $("#SGroupWrap").toggle(hasGroup);
    $("#SGroup").prop("checked", hasGroup && String($a.data("group-shuffle")) === "true").prop("disabled", partNo === 6);
    $("#SGroupHint").text(partNo === 6 ? "Part 6 luôn giữ thứ tự câu gắn với chỗ trống." : "Mặc định tắt: giữ thứ tự câu trong nhóm.");
    $("#ShuffleModal").modal("show");
  });
  $("#ShuffleForm").on("submit", function (e) {
    e.preventDefault();
    var last = null;
    var partDone = svc.examEdit.updatePart({ partId: parseInt($("#SPartId").val(), 10), shuffleItems: $("#SPart").is(":checked") });
    partDone.done(function (r1) {
      last = r1;
      var gid = $("#SGroupId").val();
      if (gid && !$("#SGroup").prop("disabled")) {
        svc.examEdit.updateGroup({ groupId: parseInt(gid, 10), shuffleQuestions: $("#SGroup").is(":checked") })
          .done(function (r2) { afterEdit(r2, $("#ShuffleModal")); });
      } else {
        afterEdit(last, $("#ShuffleModal"));
      }
    });
  });

  // ----- cài đặt đề -----
  $("#BtnSettings").on("click", function () {
    $("#SetTitle").val($root.data("title"));
    $("#SetDuration").val($root.data("duration"));
    $("#SettingsModal").modal("show");
  });
  $("#SettingsForm").on("submit", function (e) {
    e.preventDefault();
    svc.examEdit.updateSettings({ versionId: versionId, title: $("#SetTitle").val(), durationMinutes: parseInt($("#SetDuration").val(), 10) || 0 })
      .done(function (r) { afterEdit(r, $("#SettingsModal")); });
  });

  // ----- vòng đời -----
  $("#BtnValidate").on("click", function () {
    svc.examPublish.validate({ versionId: versionId }).done(function (issues) { showIssues("Kết quả kiểm tra hợp lệ", issues); });
  });
  $("#BtnPublish").on("click", function () {
    abp.message.confirm("Sau khi xuất bản, sinh viên sẽ thấy đề và được bắt đầu thi; bản này sẽ không thể sửa (muốn sửa phải tạo bản sửa). Bản đang xuất bản trước đó của đề này (nếu có) sẽ chuyển sang Lưu trữ.", "Xuất bản đề thi?", function (ok) {
      if (!ok) return;
      svc.examPublish.publish({ versionId: versionId }).done(function (r) {
        if (r.success) { abp.notify.success("Đã xuất bản."); location.reload(); }
        else { showIssues("Chưa thể xuất bản — đề còn lỗi", r.errors); }
      });
    });
  });
  $("#BtnUnpublish").on("click", function () {
    abp.message.confirm("Sinh viên sẽ không thể bắt đầu bài thi mới với phiên bản này (các lượt đang làm vẫn được hoàn thành).", "Dừng nhận thi?", function (ok) {
      if (ok) svc.examPublish.unpublish({ versionId: versionId }).done(function () { abp.notify.success("Đã dừng nhận thi."); location.reload(); });
    });
  });
  $("#BtnDraft").on("click", function () {
    svc.examPublish.createDraft({ versionId: versionId }).done(function (newId) {
      abp.notify.success("Đã tạo bản Nháp mới để chỉnh sửa.");
      location.href = abp.appPath + "Exams/Preview/" + newId;
    });
  });
  $("#BtnDelete").on("click", function () {
    abp.message.confirm("Bản Nháp sẽ bị xóa. Thao tác này không ảnh hưởng đến các phiên bản đã xuất bản.", "Xóa bản Nháp?", function (ok) {
      if (ok) svc.examPublish.deleteDraft({ versionId: versionId }).done(function () { location.href = abp.appPath + "Exams"; });
    });
  });
})(jQuery);
