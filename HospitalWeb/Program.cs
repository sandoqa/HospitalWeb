using Microsoft.EntityFrameworkCore;
using HospitalWeb.Data;

var builder = WebApplication.CreateBuilder(args);


// ===============================
// MVC
// ===============================
builder.Services.AddControllersWithViews();



// ===============================
//  ÕœÌœ ﬁ«⁄œ… «·»Ì«‰« 
// ===============================

var appDataPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "App_Data"
);


Directory.CreateDirectory(appDataPath);


var dbPath = Path.Combine(
    appDataPath,
    "hospital.db"
);



// ===============================
// ›Õ’ ﬁ«⁄œ… «·»Ì«‰« 
// ===============================

Console.WriteLine("====================================");
Console.WriteLine("Environment = " + builder.Environment.EnvironmentName);
Console.WriteLine("Content Root = " + Directory.GetCurrentDirectory());
Console.WriteLine("DB Path      = " + dbPath);
Console.WriteLine("DB Exists    = " + File.Exists(dbPath));


if (File.Exists(dbPath))
{
    FileInfo info = new FileInfo(dbPath);

    Console.WriteLine(
        "DB Size      = " + info.Length + " bytes"
    );
}

Console.WriteLine("====================================");




// ===============================
// SQLite
// ===============================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(
        $"Data Source={dbPath}"
    );
});




// ===============================
// Access Importer
// ===============================

builder.Services.AddScoped<AccessImporter>();




var app = builder.Build();




// ===============================
// «Œ »«— ﬁ«⁄œ… «·»Ì«‰« 
// ===============================

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db =
            scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();


        // ·«  ‰‘∆ ﬁ«⁄œ… ÃœÌœ… ≈–« ﬂ«‰  €Ì— „ÊÃÊœ…
        // ›ﬁÿ  Õﬁﬁ „‰ «·« ’«·
        var canConnect = db.Database.CanConnect();


        Console.WriteLine(
            "Database Connected = " + canConnect
        );


        if (canConnect)
        {
            var doctorsCount =
                db.Doctors.Count();


            Console.WriteLine(
                "Doctors Count = " + doctorsCount
            );
        }

    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "DATABASE ERROR:"
        );

        Console.WriteLine(
            ex.Message
        );
    }
}





// ===============================
// „⁄«·Ã… «·√Œÿ«¡
// ===============================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}



// ===============================
// Middleware
// ===============================

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();




// ===============================
// Default Route
// ===============================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Doctors}/{action=Index}/{id?}"
);



app.Run();