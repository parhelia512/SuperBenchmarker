﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SuperBenchmarker
{
    public class Requester
    {
        private CommandLineOptions _options;
        private TokenisedString _url;
        private HttpClient _client;
        private TemplateParser _templateParser;
        private IValueProvider _valueProvider;

        public Requester(CommandLineOptions options)
        {

            _client = new HttpClient(new HttpClientHandler()
                {
                    Proxy = WebProxy.GetDefaultProxy(),
                    UseDefaultCredentials = true
                });
            _options = options;
            _url = new TokenisedString(options.Url);
            if (!string.IsNullOrEmpty(options.Template))
            {
                _templateParser = new TemplateParser(File.ReadAllText(options.Template));
            }
            
            _valueProvider = new NoValueProvider();

            if (!string.IsNullOrEmpty(_options.Plugin)) // plugin
            {
                var assembly = Assembly.LoadFile(_options.Plugin);
                var valueProviderType = assembly.GetExportedTypes().Where(t => typeof (IValueProvider)
                                                                      .IsAssignableFrom(t)).FirstOrDefault();
                if(valueProviderType==null)
                    throw new ArgumentException("No public type in plugin implements IValueProvider.");

                _valueProvider = (IValueProvider)Activator.CreateInstance(valueProviderType);
            }
            else if (!string.IsNullOrEmpty(options.ValuesFile)) // csv values file
            {
                _valueProvider = new CsvValueProvider(options.ValuesFile);
            }

        }

        public void Next(int i)
        {
            NextAsync(i).Wait();
        }

        public async Task NextAsync(int i)
        {
            var request = BuildRequest(i);
            if (_options.Verbose)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write(request.Method.Method + " ");
                Console.WriteLine(request.RequestUri.ToString());
                Console.ResetColor();
            }
           
            try
            {
                var response = await _client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                if (_options.IsDryRun)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine(content);
                    Console.ResetColor();
                }

            }
            catch (Exception e)
            {
                if(_options.Verbose)
                    Console.WriteLine(e.ToString());
            }



        }

        internal HttpRequestMessage BuildRequest(int i)
        {
            var dictionary = GetParams(i);
            var request = new HttpRequestMessage(new HttpMethod(_options.Method), _url.ToString(dictionary));
            if (_templateParser != null)
            {
                foreach (var h in _templateParser.Headers)
                {
                    request.Headers.Add(h.Key, h.Value.ToString(dictionary));
                }
                
            }
            return request;
        }

        private IDictionary<string, object> GetParams(int i)
        {
            return _valueProvider.GetValues(i);
        }

    }
}
