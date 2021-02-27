using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.RestServer
{
    public class MemoryService
    {
        private const string PodEnvironmentVariable = "HOSTNAME";
        private const string TokenPath = @"/var/run/secrets/kubernetes.io/serviceaccount/token";
        private const string CaPath = @"/var/run/secrets/kubernetes.io/serviceaccount/ca.crt";
        private const string NamespacePath = @"/var/run/secrets/kubernetes.io/serviceaccount/namespace";
        private const string AuthSchema = "Bearer";
        private const string ApiServerBaseAddress = @"https://kubernetes.default.svc";
        private const string ApiPrefix = @"api/v1";


        //Summary api (memory usage)
        //api/v1/nodes/aks-agentpool-24059613-vmss000000/proxy/stats/summary
        //memory.available	memory.available := node.status.capacity[memory] - node.stats.memory.workingSet
        public static async Task<long> GetAllocatableSize(CancellationToken cancellationToken)
        {
            string podName = Environment.GetEnvironmentVariable(PodEnvironmentVariable);

            if (string.IsNullOrEmpty(podName))
            {
                throw new InvalidOperationException("Unable to get pod name");
            }

            //Similiar logic as the go client.
            string podNamespace = @"default";
            if (File.Exists(NamespacePath))
            {
                podNamespace = File.ReadAllText(NamespacePath);
            }

            string token = File.ReadAllText(TokenPath);

            //Kubernetes provides a Certificate Authority Bundle that can be used to validate the remote api server.
            using (var caBundle = new X509Certificate2(CaPath))
            using (var handler = new HttpClientHandler())
            {
                handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;

                handler.ServerCertificateCustomValidationCallback += (HttpRequestMessage request, X509Certificate2 leafCert, X509Chain chain, SslPolicyErrors policyErrors) =>
                {
                    //Caller performs some basic checks we can verify first. Typically this results in RemoteCertificateChainErrors.
                    if (policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable) || policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
                    {
                        return false;
                    }

                    //Parent chain will not include our Certificate authority bundle.
                    //Create a new chain to verify the server certificate.
                    using (X509Chain customChain = X509Chain.Create())
                    {
                        customChain.ChainPolicy.ExtraStore.Add(caBundle);

                        //Out CA bundle cannot verify revocations
#pragma warning disable IA5352 //Do Not Misuse Cryptographic APIs : Do Not Change X509ChainPolicy.RevocationMode to NoCheck
                        customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
#pragma warning restore IA5352
                        customChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                        return customChain.Build(leafCert);
                    }
                };

                var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri(ApiServerBaseAddress)
                };
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AuthSchema, token);

                string nodeName = null;

                using (HttpResponseMessage response = await client.GetAsync(FormattableString.Invariant($"{ApiPrefix}/namespaces/{podNamespace}/pods/{podName}"), cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    PodItem getPods = await Deserialize<PodItem>(response);
                    nodeName = getPods?.Spec?.NodeName;
                }

                if (string.IsNullOrEmpty(nodeName))
                {
                    throw new InvalidOperationException("Unable to get node name");
                }

                string memoryLimit = null;
                using (HttpResponseMessage response = await client.GetAsync(FormattableString.Invariant($"{ApiPrefix}/nodes/?fieldSelector=metadata.name%3D{nodeName}"), cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    GetNodes getNodes = await Deserialize<GetNodes>(response);
                    memoryLimit = getNodes.Items?.FirstOrDefault()?.Status?.Allocatable?.Memory;
                }

                if (string.IsNullOrEmpty(memoryLimit))
                {
                    throw new InvalidOperationException("Unable to get allocatable memory limit");
                }

                if (!Quantity.TryParse(memoryLimit, out long value))
                {
                    throw new InvalidOperationException($"Unable to parse memory limit {memoryLimit}");
                }
                return value;
            }
        }

        private static async Task<T> Deserialize<T>(HttpResponseMessage response)
        {
            using var stream = await response.Content.ReadAsStreamAsync();

            return await JsonSerializer.DeserializeAsync<T>(stream);
        }

        internal class GetPods
        {
            [JsonPropertyName("apiVersion")]
            public string ApiVersion { get; set; }

            [JsonPropertyName("items")]
            public PodItem[] Items { get; set; }
        }

        internal class PodItem
        {
            [JsonPropertyName("spec")]
            public PodSpec Spec { get; set; }
        }

        internal class PodSpec
        {
            [JsonPropertyName("nodeName")]
            public string NodeName { get; set; }
        }

        internal class GetNodes
        {
            [JsonPropertyName("apiVersion")]
            public string ApiVersion { get; set; }

            [JsonPropertyName("items")]
            public NodeItem[] Items { get; set; }
        }

        internal class NodeItem
        {
            [JsonPropertyName("status")]
            public NodeStatus Status { get; set; }
        }

        internal class NodeStatus
        {
            [JsonPropertyName("allocatable")]
            public MemoryLimit Allocatable { get; set; }
        }

        internal class MemoryLimit
        {
            [JsonPropertyName("memory")]
            public string Memory { get; set; }
        }
    }
}
