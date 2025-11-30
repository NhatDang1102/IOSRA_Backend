using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Helpers
{
    public class ElevenLabsClient : IElevenLabsClient
    {
        private readonly HttpClient _httpClient;
        private readonly ElevenLabsSettings _settings;
        private readonly ILogger<ElevenLabsClient> _logger;

        public ElevenLabsClient(HttpClient httpClient, IOptions<ElevenLabsSettings> options, ILogger<ElevenLabsClient> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task<byte[]> SynthesizeAsync(string voiceId, string text, CancellationToken ct = default)
        {
            var payload = new
            {
                text,
                model_id = string.IsNullOrWhiteSpace(_settings.ModelId) ? "eleven_flash_v2_5" : _settings.ModelId,
                voice_settings = new
                {
                    stability = _settings.Stability ?? 0.35,
                    similarity_boost = _settings.SimilarityBoost ?? 0.75
                },
                output_format = string.IsNullOrWhiteSpace(_settings.OutputFormat) ? "mp3_44100_128" : _settings.OutputFormat
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync($"v1/text-to-speech/{voiceId}", payload, cancellationToken: ct);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("ElevenLabs synthesis failed: {Status} - {Body}", response.StatusCode, body);
                    throw new AppException("VoiceSynthesisFailed", "Failed to generate voice audio.", (int)response.StatusCode);
                }

                return await response.Content.ReadAsByteArrayAsync(ct);
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ElevenLabs synthesis threw an exception");
                throw new AppException("VoiceSynthesisError", "Voice synthesis service is unavailable. Please try again later.", 502);
            }
        }

        public async Task<byte[]> ComposeMusicAsync(string prompt, int lengthMs = 30000, string? outputFormat = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt must not be empty.", nameof(prompt));
            }

            var payload = new
            {
                prompt,
                music_length_ms = Math.Clamp(lengthMs, 3000, 300000),
                output_format = string.IsNullOrWhiteSpace(outputFormat) ? _settings.MusicOutputFormat : outputFormat,
                force_instrumental = _settings.ForceInstrumental
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("v1/music", payload, cancellationToken: ct);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("ElevenLabs music compose failed: {Status} - {Body}", response.StatusCode, body);
                    throw new AppException("MusicGenerationFailed", "Failed to generate mood music.", (int)response.StatusCode);
                }

                return await response.Content.ReadAsByteArrayAsync(ct);
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ElevenLabs music compose threw an exception");
                throw new AppException("MusicGenerationError", "Music generation service is unavailable. Please try again later.", 502);
            }
        }
    }
}
