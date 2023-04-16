using Microsoft.Xrm.Sdk;
using System;
using System.Net.Http;
using Newtonsoft.Json.Linq;


namespace PluginBuscaPorCEP
{
    public class PCEP : IPlugin
    {
        private readonly string _unsecureConfig;

        public PCEP(string unsecureConfig)
        {
            _unsecureConfig = unsecureConfig;
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);
            var tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracingService.Trace("Plugin BuscaPorCEP iniciado.");

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                var entity = (Entity)context.InputParameters["Target"];

                if (entity.LogicalName != "account" && entity.LogicalName != "contact")
                {
                    tracingService.Trace("O plugin deve ser executado somente para as entidades 'account' e 'contact'.");
                    return;
                }

                var cep = entity.GetAttributeValue<string>("address1_postalcode");

                if (!string.IsNullOrEmpty(cep))
                {
                    try
                    {
                        var url = $"https://viacep.com.br/ws/{cep}/json/";
                        var client = new HttpClient();
                        var response = client.GetAsync(url).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            var content = response.Content.ReadAsStringAsync().Result;
                            var data = JObject.Parse(content);

                            tracingService.Trace($"CEP: {cep}");
                            tracingService.Trace($"Cidade: {data["localidade"]}");
                            tracingService.Trace($"Estado: {data["uf"]}");
                            tracingService.Trace($"Logradouro: {data["logradouro"]}");

                            var update = new Entity(entity.LogicalName, entity.Id);
                            update["address1_city"] = data["localidade"].ToString();
                            update["address1_stateorprovince"] = data["uf"].ToString();
                            update["address1_line1"] = data["logradouro"].ToString();

                            service.Update(update);
                        }
                        else
                        {
                            tracingService.Trace($"Erro ao buscar o CEP: {cep}");
                        }
                    }
                    catch (Exception ex)
                    {
                        tracingService.Trace($"Erro ao buscar o CEP: {cep}. Detalhes: {ex.Message}");
                        throw new InvalidPluginExecutionException($"Ocorreu um erro durante a busca de CEP: {ex.Message}");
                    }
                }
            }
        }
    }
}