using Opc.Ua;
using Opc.Ua.Configuration;
using OPC_UA_SERVER_DEMO;
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Utils.SetTraceOutput(Utils.TraceOutput.DebugAndFile);      // Включаем лог
        Utils.SetTraceMask(511);                                   // Максимальная детализация лога

        var app = new ApplicationInstance
        {
            ApplicationName = "MyOpcUaServer",
            ApplicationType = ApplicationType.Server,
            ConfigSectionName = "MyOpcUaServer"
        };

        // Конфигурация с актуальным именем хоста
        app.ApplicationConfiguration = CreateApplicationConfiguration();

        // Проверка и создание сертификата, если не найден
        await app.CheckApplicationInstanceCertificate(false, 0);

        // Проверка наличия валидного сертификата
        if (app.ApplicationConfiguration.SecurityConfiguration.AutoAcceptUntrustedCertificates == false)
        {
            if (app.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.Certificate == null)
            {
                Console.WriteLine("Ошибка: не удалось создать/загрузить сертификат.");
                return;
            }
        }

        // Запуск OPC UA сервера
        await app.Start(new MyServer());

        Console.WriteLine("Сервер запущен на: opc.tcp://localhost:4840/");
        Console.WriteLine("Нажмите Enter для выхода...");
        Console.ReadLine();
    }

    public static ApplicationConfiguration CreateApplicationConfiguration()
    {
        string hostname = Utils.GetHostName();  // Автоматически определяем имя текущего ПК

        var config = new ApplicationConfiguration
        {
            ApplicationName = "MyOpcUaServer",
            ApplicationUri = $"urn:{hostname}:MyOpcUaServer",      // Важно! Динамическое имя хоста
            ApplicationType = ApplicationType.Server,

            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = @"CertificateStores\Own",
                    SubjectName = "CN=MyOpcUaServer"
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = @"CertificateStores\UA Applications"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = @"CertificateStores\UA Certificate Authorities"
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = @"CertificateStores\RejectedCertificates"
                },

                AutoAcceptUntrustedCertificates = true, // ← На время отладки
                AddAppCertToTrustedStore = true
            },

            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 15000,
                MaxStringLength = 65535,
                MaxByteStringLength = 65535,
                MaxArrayLength = 65535,
                MaxMessageSize = 4 * 1024 * 1024,
                MaxBufferSize = 65535,
                ChannelLifetime = 600000,
                SecurityTokenLifetime = 3600000
            },

            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = { "opc.tcp://localhost:4840" }, // Можешь заменить на IP:4840
                SecurityPolicies = {
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.None,
                        SecurityPolicyUri = SecurityPolicies.None
                    }
                },
                MaxSessionCount = 100,
                MinSessionTimeout = 10000,
                MaxSessionTimeout = 600000,
                MaxBrowseContinuationPoints = 10,
                MaxQueryContinuationPoints = 10,
                MaxHistoryContinuationPoints = 10
            },

            DisableHiResClock = false
        };

        // Убедиться, что конфигурация валидна
        config.Validate(ApplicationType.Server).Wait();

        // Создать директории, если не существуют
        EnsureCertificateDirsExist(config.SecurityConfiguration);

        return config;
    }

    private static void EnsureCertificateDirsExist(SecurityConfiguration securityConfig)
    {
        void CreateIfNotExist(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        CreateIfNotExist(securityConfig.ApplicationCertificate.StorePath);
        CreateIfNotExist(securityConfig.TrustedPeerCertificates.StorePath);
        CreateIfNotExist(securityConfig.TrustedIssuerCertificates.StorePath);
        CreateIfNotExist(securityConfig.RejectedCertificateStore.StorePath);
    }
}
