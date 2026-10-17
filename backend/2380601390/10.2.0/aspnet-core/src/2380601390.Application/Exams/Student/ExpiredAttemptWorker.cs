using System;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.Threading;
using Abp.Threading.BackgroundWorkers;
using Abp.Threading.Timers;
using _2380601390.Exams.Attempts;

namespace _2380601390.Exams.Student;

/// <summary>
/// Tự thu bài các lượt đã hết hạn kể cả khi client đã đóng (chạy định kỳ ~30 giây).
/// Thao tác thu bài là UPDATE có điều kiện nên chạy trùng với nộp tay hoặc nhiều instance vẫn chỉ chấm một lần.
/// </summary>
public class ExpiredAttemptWorker : PeriodicBackgroundWorkerBase, ISingletonDependency
{
    public ExpiredAttemptWorker(AbpTimer timer) : base(timer)
    {
        Timer.Period = 30 * 1000;
    }

    protected override void DoWork()
    {
        using (var mgr = IocManager.Instance.ResolveAsDisposable<ExamAttemptManager>())
        {
            System.Collections.Generic.List<long> ids;
            using (var uow = UnitOfWorkManager.Begin())
            {
                ids = mgr.Object.GetExpiredAttemptIds(50);
                uow.Complete();
            }

            foreach (var id in ids)
            {
                try
                {
                    using (var uow = UnitOfWorkManager.Begin())
                    {
                        AsyncHelper.RunSync(() => mgr.Object.FinalizeAsync(id));
                        uow.Complete();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Không thu được bài của lượt thi " + id, ex);
                }
            }
        }
    }
}
