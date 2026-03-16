using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LazyBootstrap.Services.Network
{
    internal static class NetworkAdapterDiscovery
    {
        internal sealed class NetworkAdapterInfo
        {
            public NetworkAdapterInfo(string displayName, string ipAddress, string subnetMask)
            {
                DisplayName = displayName ?? string.Empty;
                IpAddress = ipAddress ?? string.Empty;
                SubnetMask = subnetMask ?? string.Empty;
            }

            public string DisplayName { get; }

            public string IpAddress { get; }

            public string SubnetMask { get; }
        }

        public static IReadOnlyList<NetworkAdapterInfo> GetAvailableAdapters()
        {
            var result = new List<NetworkAdapterInfo>();

            try
            {
                foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback
                        || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    var properties = networkInterface.GetIPProperties();
                    foreach (var unicastAddress in properties.UnicastAddresses)
                    {
                        if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork)
                        {
                            continue;
                        }

                        var ipAddress = unicastAddress.Address?.ToString() ?? string.Empty;
                        var subnetMask = unicastAddress.IPv4Mask?.ToString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(subnetMask))
                        {
                            continue;
                        }

                        var adapterName = string.IsNullOrWhiteSpace(networkInterface.Name)
                            ? networkInterface.Description ?? "未知网卡"
                            : networkInterface.Name;
                        var description = string.IsNullOrWhiteSpace(networkInterface.Description)
                            || string.Equals(adapterName, networkInterface.Description, StringComparison.OrdinalIgnoreCase)
                            ? string.Empty
                            : $" - {networkInterface.Description}";
                        var displayName = $"{adapterName}{description} ({ipAddress} / {subnetMask})";
                        result.Add(new NetworkAdapterInfo(displayName, ipAddress, subnetMask));
                    }
                }
            }
            catch
            {
            }

            return result
                .OrderBy(adapter => adapter.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
