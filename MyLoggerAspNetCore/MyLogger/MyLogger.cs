﻿using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyLogger
{
    public class MyLogger : IMyLogger
    {
        string _categoryName { get; set; }
        MyLoggerOption _option;
        HubConnection connection;

        #region =====构造函数=====
        public MyLogger(IOptions<MyLoggerOption> wxOption) : this(wxOption.Value, "")
        {
        }

        public MyLogger(MyLoggerOption option, string categoryName)
        {
            _option = option;
            _categoryName = categoryName;

            connection = new HubConnectionBuilder()
                //.WithUrl("http://monitor.imady.com/MyLoggerHub")
                .WithUrl(_option.MyLoggerHubUrl)
                .Build();

            connection.Closed += async (error) =>
            {
                await Task.Delay(2000);
                await connection.StartAsync();
            };
            connection.StartAsync().Wait();

        }
        #endregion


        #region =====ILogger实现=====
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _option.LogLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var task = connection.SendAsync(
                "OnILogging",
                DateTime.Now,
                _option.ProjectName,
                _categoryName,
                logLevel.ToString(),
                //new { Level = logLevel, Content = formatter(state, exception) });
                formatter(state, exception));
            task.Wait();
        }
        #endregion


        #region =====IMyLogger扩展实现=====
        public void LogCustom(string sender, string message)
        {
            //this._categoryName = sender;
            this.LogInformation(message);
        }

        /// <summary>
        /// 发送异常，以标准方式将Exception序列化后发送。
        /// </summary>
        /// <param name="exception"></param>
        public void LogException(Exception exception)
        {
            //Frank: exception已经被序列化，是为了减少服务器端拆箱/装箱的开销。
            //故此要求抛出exception的源需要将异常信息序列化后再传输。
            var formatter = new Func<Exception, string>(ExceptionSerializer);
            var message = $"{(exception.Message==null?" ":exception.Message)}\r\nStackTrace: {(exception.StackTrace==null?" ":exception.StackTrace)}\r\nInnerException:{(exception.InnerException != null ? exception.InnerException.Message : exception.Message)}";
            var task = connection.SendAsync(
                "OnMyLogException",
                DateTime.Now,
                _option.ProjectName,
                _categoryName,
                "Exception",//微软官方6种Log级别以外扩展出来的级别
                formatter(exception)
                );
            task.Wait();
        }

        private string ExceptionSerializationResult;
        private string ExceptionSerializer(Exception arg)
        {
            ExceptionSerializationResult += $"{arg.Message} \r\n";
            ExceptionSerializationResult += $"Source: {arg.Source} \r\n";
            ExceptionSerializationResult += $"StackTrace: {arg.StackTrace} \r\n";


            if (arg.InnerException != null)
            {
                var formatter = new Func<Exception, string>(ExceptionSerializer);
                ExceptionSerializationResult += formatter(arg.InnerException);
            }
            return ExceptionSerializationResult;
        }

        /// <summary>
        /// 发送异常，允许调用者提供一个序列化器
        /// </summary>
        /// <param name="exception">异常消息体</param>
        /// <param name="formatter">调用者自己提供的客制化序列化器</param>
        public void LogException(Exception exception, Func<Exception, string> formatter)
        {
            //Frank: exception已经被序列化，是为了减少服务器端拆箱/装箱的开销。
            //故此要求抛出exception的源需要将异常信息序列化后再传输。

            var task = connection.SendAsync(
                "OnMyLogException",
                DateTime.Now,
                _option.ProjectName,
                _categoryName,
                "Exception",
                //new { Level = logLevel, Content = formatter(state, exception) });
                formatter(exception)
                );
            task.Wait();
        }
        #endregion


        #region =====支撑方法=====
        private void Logging(HubConnection connection, string PortName, DateTime time, params string[] parameters)
        {
            connection.StartAsync().Wait();
        }
        /*
        try
        {
            connection.StartAsync();
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        connection.InvokeAsync("SendMessage", "Frank","MyLogger test");
        */


        #endregion
    }




    public class MyLogger<TCategory> : MyLogger, IMyLogger<TCategory>
    {
        #region =====构造函数=====
        public MyLogger(IOptions<MyLoggerOption> wxOption) : base(wxOption.Value, typeof(TCategory).Name)
        {
        }


        #endregion
    }

}
