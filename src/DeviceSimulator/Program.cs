using System.Net.Http.Json;

var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};
var http = new HttpClient(handler);

http.DefaultRequestHeaders.Add("x-api-key", "local-dev");

var random = new Random();
var deviceId = "sim-01";
var patientId = "p-123";

Console.WriteLine("DeviceSimulator started. Posting measurements every 2s...");

while (true)
{
    var hr = new
    {
        MeasurementId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        DeviceId = deviceId,
        PatientId = patientId,
        Type = "HeartRate",
        Value = random.Next(58, 98),
        Unit = "bpm"
    };

    var response = await http.PostAsJsonAsync("http://localhost:5100/api/v1/measurements", hr);
    Console.WriteLine($"[{hr.Timestamp:HH:mm:ss}] Posted HeartRate={hr.Value} bpm → {(int)response.StatusCode}");

    await Task.Delay(TimeSpan.FromSeconds(2));
}