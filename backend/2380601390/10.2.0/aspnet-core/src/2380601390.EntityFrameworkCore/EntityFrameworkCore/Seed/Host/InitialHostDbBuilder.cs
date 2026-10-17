namespace _2380601390.EntityFrameworkCore.Seed.Host;

public class InitialHostDbBuilder
{
    private readonly _2380601390DbContext _context;

    public InitialHostDbBuilder(_2380601390DbContext context)
    {
        _context = context;
    }

    public void Create()
    {
        new DefaultEditionCreator(_context).Create();
        new DefaultLanguagesCreator(_context).Create();
        new HostRoleAndUserCreator(_context).Create();
        new ExamRolesAndUsersCreator(_context).Create();
        new DefaultSettingsCreator(_context).Create();

        _context.SaveChanges();
    }
}
