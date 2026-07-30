using Microsoft.EntityFrameworkCore;
using HospitalWeb.Data;

var builder = WebApplication.CreateBuilder(args);

// ≈÷«›… MVC
builder.Services.AddControllersWithViews();

//  ÕœÌœ „”«— ﬁ«⁄œ… «·»Ì«‰« 
var dbPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "App_Data",
    "hospital.db"
);

// ·· √ﬂœ „‰ «·„”«— ⁄·Ï Render
Console.WriteLine("====================================");
Console.WriteLine("DB Path   = " + dbPath);
Console.WriteLine("DB Exists = " + File.Exists(dbPath));
if (File.Exists(dbPath))
{
    var info = new FileInfo(dbPath);
    Console.WriteLine("DB Size   = " + info.Length + " bytes");
}
Console.WriteLine("====================================");

// ≈‰‘«¡ „Ã·œ App_Data ≈–« ·„ Ìﬂ‰ „ÊÃÊœ«
Directory.CreateDirectory(
    Path.GetDirectoryName(dbPath)!
);

// —»ÿ SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

//  ”ÃÌ· „” Ê—œ Access
builder.Services.AddScoped<AccessImporter>();

var app = builder.Build();

// ≈‰‘«¡ ﬁ«⁄œ… «·»Ì«‰«  ≈–« ·„  ﬂ‰ „ÊÃÊœ…
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    db.Database.EnsureCreated();

    // ·· √ﬂœ „‰ ⁄œœ «·√ÿ»«¡
    Console.WriteLine("Doctors Count = " + db.Doctors.Count());
}

// „⁄«·Ã… «·√Œÿ«¡
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// «·’›Õ… «·—∆Ì”Ì…
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Doctors}/{action=Index}/{id?}");

app.Run();