using Backeddy.Core;
using Backeddy.Service;

// Backeddy — serviço de backup. Instalado pelo instalador como "Backeddy" (início automático, como sistema).
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = AppInfo.ServiceName);
builder.Services.AddSingleton<StatusStore>();
builder.Services.AddHostedService<PipeServer>();
builder.Build().Run();
