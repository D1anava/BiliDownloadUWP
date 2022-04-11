﻿using BiliDownload.Helper;
using BiliDownload.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;
using XC.RSAUtil;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace BiliDownload.HelperPage
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class PwdLoginPage : Page
    {
        public string UserName { get; set; }
        public string PasswordHash { get; set; }
        public string Key { get; set; }
        public string Challenge { get; set; }
        public string Validate { get; set; }
        public string Seccode { get; set; }
        public PwdLoginPage()
        {
            this.InitializeComponent();
            this.pwdPasswordBox.KeyDown += (s, e) => //回车登录
            {
                if (e.Key == Windows.System.VirtualKey.Enter) loginBtn_Click(loginBtn, new RoutedEventArgs());
            };
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        private async Task<(string, string, string)> GetCaptchaCodeAsync()
        {
            var json = JsonConvert.DeserializeObject<CaptchaJson>(await NetHelper.HttpGet("http://passport.bilibili.com/web/captcha/combine?plat=6", null, null));
            return (json.data.result.gt, json.data.result.challenge, json.data.result.key);
        }

        private async void loginBtn_Click(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrWhiteSpace(userNameTextBox.Text)) return;
            if (string.IsNullOrWhiteSpace(pwdPasswordBox.Password)) return;

            var captchaCodes = await GetCaptchaCodeAsync();
            var gt = captchaCodes.Item1;
            var challenge = captchaCodes.Item2;
            var key = captchaCodes.Item3;

            this.Challenge = challenge;
            this.Key = key;

            this.loginWebView.WebMessageReceived += LoginWebView_WebMessageReceived;
            this.loginWebView.NavigationCompleted += async (s, e) =>
            {
                this.DispatcherQueue.TryEnqueue(() => this.loginWebView.Visibility = Visibility.Visible);
                await this.loginWebView.ExecuteScriptAsync($"reg('{gt}','{challenge}');");
            };
            await this.loginWebView.EnsureCoreWebView2Async();
            this.loginWebView.NavigateToString(html);

            this.progressRing.Visibility = Visibility.Visible;
            (sender as Button).IsEnabled = false;
        }

        private void LoginWebView_WebMessageReceived(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
        {
            var e = new { Value = args.WebMessageAsJson.Replace("\"", "") };
            this.Validate = e.Value[..e.Value.LastIndexOf('&')];
            this.Seccode = e.Value[(e.Value.LastIndexOf('&') + 1)..];
            this.DispatcherQueue.TryEnqueue(
                async () =>
                {
                    this.loginWebView.Visibility = Visibility.Collapsed;
                    this.progressRing.Visibility = Visibility.Collapsed;
                    this.UserName = userNameTextBox.Text;
                    this.PasswordHash = await GeneratePwdHash(this.pwdPasswordBox.Password);

                    var postContent = $"captchaType=6&username={UrlEncode(UserName)}&keep=true&key={UrlEncode(this.Key)}&challenge={UrlEncode(Challenge)}&validate={UrlEncode(Validate)}&seccode={UrlEncode(Seccode)}&password={UrlEncode(PasswordHash)}";

                    var result = await NetHelper.HttpPostAsync
                        ("http://passport.bilibili.com/web/login/v2", null, postContent);
                    var json = JsonConvert.DeserializeObject<LoginJson>(result.Item1);
                    if (json.code != 0) { this.errorTextBlock.Text = json.code.ToString(); this.loginBtn.IsEnabled = true; }
                    else if (json.code == 0)
                    {
                        var sESSDATA = Regex.Match(result.Item2.ToString(), "(?<=SESSDATA=)[%|a-z|A-Z|0-9|*]*")?.Value;
                        var uid = Regex.Match(result.Item2.ToString(), "(?<=DedeUserID=)[0-9]*")?.Value;
                        ApplicationData.Current.LocalSettings.Values["biliUserSESSDATA"] = sESSDATA;
                        ApplicationData.Current.LocalSettings.Values["biliUserUid"] = long.Parse(uid);
                        ApplicationData.Current.LocalSettings.Values["isLogined"] = true;
                        UserLoginPage.Current.PwdLoginOk();
                    }
                });
        }

        private void cancelBtn_Click(object sender, RoutedEventArgs e)
        {
            UserLoginPage.Current.PwdLoginCancel();
        }
        private async Task<string> GeneratePwdHash(string pwd)
        {
            var json = JsonConvert.DeserializeObject<HashJson>(await NetHelper.HttpGet("http://passport.bilibili.com/login?act=getkey", null, null));
            var before = json.hash + pwd;
            var rsa = new RsaPkcs1Util(Encoding.UTF8, json.key);
            var after = rsa.Encrypt(before, RSAEncryptionPadding.Pkcs1);
            return after;
        }
        private string UrlEncode(string content)
        {
            return content.Replace("%", "%25").Replace("+", "%2B").Replace("/", "%2F").Replace(":", "%3A")
                .Replace("|", "%7C").Replace("?", "%3F").Replace("#", "%23").Replace("&", "%26").Replace("=", "%3D");
        }
        /// <summary>
        /// Copied from validator.html
        /// </summary>
        private const string html = @"
<!DOCTYPE html>
<html lang='en'>

<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Document</title>
    <style>
        body {
            margin: 50px 0;
            text-align: center;
            font-family: 'PingFangSC-Regular', 'Open Sans', Arial, 'Hiragino Sans GB', 'Microsoft YaHei', 'STHeiti', 'WenQuanYi Micro Hei', SimSun, sans-serif;
        }

        .inp {
            border: 1px solid #cccccc;
            border-radius: 2px;
            padding: 0 10px;
            width: 320px;
            height: 40px;
            font-size: 18px;
        }

        .btn {
            display: inline-block;
            box-sizing: border-box;
            border: 1px solid #cccccc;
            border-radius: 2px;
            width: 100px;
            height: 40px;
            line-height: 40px;
            font-size: 16px;
            color: #666;
            cursor: pointer;
            background: white linear-gradient(180deg, #ffffff 0%, #f3f3f3 100%);
        }

        .btn:hover {
            background: white linear-gradient(0deg, #ffffff 0%, #f3f3f3 100%)
        }

        #captcha {
            width: 300px;
            display: inline-block;
        }

        label {
            vertical-align: top;
            display: inline-block;
            width: 120px;
            text-align: right;
        }

        #text {
            height: 42px;
            width: 298px;
            text-align: center;
            border-radius: 2px;
            background-color: #F3F3F3;
            color: #BBBBBB;
            font-size: 14px;
            letter-spacing: 0.1px;
            line-height: 42px;
        }

        #wait {
            display: none;
            height: 42px;
            width: 298px;
            text-align: center;
            border-radius: 2px;
            background-color: #F3F3F3;
        }

        .loading {
            margin: auto;
            width: 70px;
            height: 20px;
        }

        .loading-dot {
            float: left;
            width: 8px;
            height: 8px;
            margin: 18px 4px;
            background: #ccc;

            -webkit-border-radius: 50%;
            -moz-border-radius: 50%;
            border-radius: 50%;

            opacity: 0;

            -webkit-box-shadow: 0 0 2px black;
            -moz-box-shadow: 0 0 2px black;
            -ms-box-shadow: 0 0 2px black;
            -o-box-shadow: 0 0 2px black;
            box-shadow: 0 0 2px black;

            -webkit-animation: loadingFade 1s infinite;
            -moz-animation: loadingFade 1s infinite;
            animation: loadingFade 1s infinite;
        }

        .loading-dot:nth-child(1) {
            -webkit-animation-delay: 0s;
            -moz-animation-delay: 0s;
            animation-delay: 0s;
        }

        .loading-dot:nth-child(2) {
            -webkit-animation-delay: 0.1s;
            -moz-animation-delay: 0.1s;
            animation-delay: 0.1s;
        }

        .loading-dot:nth-child(3) {
            -webkit-animation-delay: 0.2s;
            -moz-animation-delay: 0.2s;
            animation-delay: 0.2s;
        }

        .loading-dot:nth-child(4) {
            -webkit-animation-delay: 0.3s;
            -moz-animation-delay: 0.3s;
            animation-delay: 0.3s;
        }

        @-webkit-keyframes loadingFade {
            0% { opacity: 0; }
            50% { opacity: 0.8; }
            100% { opacity: 0; }
        }

        @-moz-keyframes loadingFade {
            0% { opacity: 0; }
            50% { opacity: 0.8; }
            100% { opacity: 0; }
        }

        @keyframes loadingFade {
            0% { opacity: 0; }
            50% { opacity: 0.8; }
            100% { opacity: 0; }
        }
    </style>
</head>

<body>

    <div>请进行人机验证，按生成以获取验证码</div>
    <div>如果加载太久，请刷新重试，请勿打开多个登录窗口</div>

    <div id='btn-gen' class='btn'>生成</div>
    <br><br>

    <div>
        <div id='captcha'>
            <div id='text'>
                请先生成
            </div>
            <div id='wait' class='show'>
                <div class='loading'>
                    <div class='loading-dot'></div>
                    <div class='loading-dot'></div>
                    <div class='loading-dot'></div>
                    <div class='loading-dot'></div>
                </div>
            </div>
        </div>
    </div>
    <br>

    <div>通过人机验证后，点击完成以登录</div>

    <div id='btn-result' class='btn'>完成</div>

    <script>
        'v0.4.8 Geetest Inc.';

        (function (window) {
            'use strict';
            if (typeof window === 'undefined') {
                throw new Error('Geetest requires browser environment');
            }

        var document = window.document;
        var Math = window.Math;
        var head = document.getElementsByTagName('head')[0];

        function _Object(obj) {
            this._obj = obj;
        }

        _Object.prototype = {
            _each: function (process) {
                var _obj = this._obj;
                for (var k in _obj) {
                    if (_obj.hasOwnProperty(k)) {
                        process(k, _obj[k]);
                    }
                }
                return this;
            }
        };

        function Config(config) {
            var self = this;
            new _Object(config)._each(function (key, value) {
                self[key] = value;
            });
        }

        Config.prototype = {
            api_server: 'api.geetest.com',
            protocol: 'http://',
            typePath: '/gettype.php',
            fallback_config: {
                slide: {
                    static_servers: ['static.geetest.com', 'dn-staticdown.qbox.me'],
                    type: 'slide',
                    slide: '/static/js/geetest.0.0.0.js'
                },
                fullpage: {
                    static_servers: ['static.geetest.com', 'dn-staticdown.qbox.me'],
                    type: 'fullpage',
                    fullpage: '/static/js/fullpage.0.0.0.js'
                }
            },
            _get_fallback_config: function () {
                var self = this;
                if (isString(self.type)) {
                    return self.fallback_config[self.type];
                } else if (self.new_captcha) {
                    return self.fallback_config.fullpage;
                } else {
                    return self.fallback_config.slide;
                }
            },
            _extend: function (obj) {
                var self = this;
                new _Object(obj)._each(function (key, value) {
                    self[key] = value;
                })
            }
        };
        var isNumber = function (value) {
            return (typeof value === 'number');
        };
        var isString = function (value) {
            return (typeof value === 'string');
        };
        var isBoolean = function (value) {
            return (typeof value === 'boolean');
        };
        var isObject = function (value) {
            return (typeof value === 'object' && value !== null);
        };
        var isFunction = function (value) {
            return (typeof value === 'function');
        };
        var MOBILE = /Mobi/i.test(navigator.userAgent);
        var pt = MOBILE ? 3 : 0;

        var callbacks = {};
        var status = {};

        var nowDate = function () {
            var date = new Date();
            var year = date.getFullYear();
            var month = date.getMonth() + 1;
            var day = date.getDate();
            var hours = date.getHours();
            var minutes = date.getMinutes();
            var seconds = date.getSeconds();

            if (month >= 1 && month <= 9) {
            month = '0' + month;
            }
            if (day >= 0 && day <= 9) {
            day = '0' + day;
            }
            if (hours >= 0 && hours <= 9) {
            hours = '0' + hours;
            }
            if (minutes >= 0 && minutes <= 9) {
            minutes = '0' + minutes;
            }
            if (seconds >= 0 && seconds <= 9) {
            seconds = '0' + seconds;
            }
            var currentdate = year + '-' + month + '-' + day + ' ' + hours + ':' + minutes + ':' + seconds;
            return currentdate;
        }

        var random = function () {
            return parseInt(Math.random() * 10000) + (new Date()).valueOf();
        };

        var loadScript = function (url, cb) {
            var script = document.createElement('script');
            script.charset = 'UTF-8';
            script.async = true;

            // 对geetest的静态资源添加 crossOrigin
            if ( /static\.geetest\.com/g.test(url)) {
                script.crossOrigin = 'anonymous';
            }

            script.onerror = function () {
                cb(true);
            };
            var loaded = false;
            script.onload = script.onreadystatechange = function () {
                if (!loaded &&
                    (!script.readyState ||
                    'loaded' === script.readyState ||
                    'complete' === script.readyState)) {

                    loaded = true;
                    setTimeout(function () {
                        cb(false);
                    }, 0);
                }
            };
            script.src = url;
            head.appendChild(script);
        };

        var normalizeDomain = function (domain) {
            // special domain: uems.sysu.edu.cn/jwxt/geetest/
            // return domain.replace(/^https?:\/\/|\/.*$/g, ''); uems.sysu.edu.cn
            return domain.replace(/^https?:\/\/|\/$/g, ''); // uems.sysu.edu.cn/jwxt/geetest
        };
        var normalizePath = function (path) {
            path = path.replace(/\/+/g, '/');
            if (path.indexOf('/') !== 0) {
                path = '/' + path;
            }
            return path;
        };
        var normalizeQuery = function (query) {
            if (!query) {
                return '';
            }
            var q = '?';
            new _Object(query)._each(function (key, value) {
                if (isString(value) || isNumber(value) || isBoolean(value)) {
                    q = q + encodeURIComponent(key) + '=' + encodeURIComponent(value) + '&';
                }
            });
            if (q === '?') {
                q = '';
            }
            return q.replace(/&$/, '');
        };
        var makeURL = function (protocol, domain, path, query) {
            domain = normalizeDomain(domain);

            var url = normalizePath(path) + normalizeQuery(query);
            if (domain) {
                url = protocol + domain + url;
            }

            return url;
        };

        var load = function (config, send, protocol, domains, path, query, cb) {
            var tryRequest = function (at) {

                var url = makeURL(protocol, domains[at], path, query);
                loadScript(url, function (err) {
                    if (err) {
                        if (at >= domains.length - 1) {
                            cb(true);
                            // report gettype error
                            if (send) {
                                config.error_code = 508;
                                var url = protocol + domains[at] + path;
                                reportError(config, url);
                            }
                        } else {
                            tryRequest(at + 1);
                        }
                    } else {
                        cb(false);
                    }
                });
            };
            tryRequest(0);
        };


        var jsonp = function (domains, path, config, callback) {
            if (isObject(config.getLib)) {
                config._extend(config.getLib);
                callback(config);
                return;
            }
            if (config.offline) {
                callback(config._get_fallback_config());
                return;
            }

            var cb = 'geetest_' + random();
            window[cb] = function (data) {
                if (data.status == 'success') {
                    callback(data.data);
                } else if (!data.status) {
                    callback(data);
                } else {
                    callback(config._get_fallback_config());
                }
                window[cb] = undefined;
                try {
                    delete window[cb];
                } catch (e) {
                }
            };
            load(config, true, config.protocol, domains, path, {
                gt: config.gt,
                callback: cb
            }, function (err) {
                if (err) {
                    callback(config._get_fallback_config());
                }
            });
        };

        var reportError = function (config, url) {
            load(config, false, config.protocol, ['monitor.geetest.com'], '/monitor/send', {
                time: nowDate(),
                captcha_id: config.gt,
                challenge: config.challenge,
                pt: pt,
                exception_url: url,
                error_code: config.error_code
            }, function (err) {})
        }

        var throwError = function (errorType, config) {
            var errors = {
                networkError: '网络错误',
                gtTypeError: 'gt字段不是字符串类型'
            };
            if (typeof config.onError === 'function') {
                config.onError(errors[errorType]);
            } else {
                throw new Error(errors[errorType]);
            }
        };

        var detect = function () {
            return window.Geetest || document.getElementById('gt_lib');
        };

        if (detect()) {
            status.slide = 'loaded';
        }

        window.initGeetest = function (userConfig, callback) {

            var config = new Config(userConfig);

            if (userConfig.https) {
                config.protocol = 'https://';
            } else if (!userConfig.protocol) {
                config.protocol = window.location.protocol + '//';
            }

            // for KFC
            if (userConfig.gt === '050cffef4ae57b5d5e529fea9540b0d1' ||
                userConfig.gt === '3bd38408ae4af923ed36e13819b14d42') {
                config.apiserver = 'yumchina.geetest.com/'; // for old js
                config.api_server = 'yumchina.geetest.com';
            }

            if(userConfig.gt){
                window.GeeGT = userConfig.gt
            }

            if(userConfig.challenge){
                window.GeeChallenge = userConfig.challenge
            }

            if (isObject(userConfig.getType)) {
                config._extend(userConfig.getType);
            }
            jsonp([config.api_server || config.apiserver], config.typePath, config, function (newConfig) {
                var type = newConfig.type;
                var init = function () {
                    config._extend(newConfig);
                    callback(new window.Geetest(config));
                };

                callbacks[type] = callbacks[type] || [];
                var s = status[type] || 'init';
                if (s === 'init') {
                    status[type] = 'loading';

                    callbacks[type].push(init);

                    load(config, true, config.protocol, newConfig.static_servers || newConfig.domains, newConfig[type] || newConfig.path, null, function (err) {
                        if (err) {
                            status[type] = 'fail';
                            throwError('networkError', config);
                        } else {
                            status[type] = 'loaded';
                            var cbs = callbacks[type];
                            for (var i = 0, len = cbs.length; i < len; i = i + 1) {
                                var cb = cbs[i];
                                if (isFunction(cb)) {
                                    cb();
                                }
                            }
                            callbacks[type] = [];
                        }
                    });
                } else if (s === 'loaded') {
                    init();
                } else if (s === 'fail') {
                    throwError('networkError', config);
                } else if (s === 'loading') {
                    callbacks[type].push(init);
                }
            });

        };


        })(window);


    </script>

    <script>
        var gt;
        var challenge;

        function reg(g, c)
        {
            gt = g;
            challenge = c;
        }

        var handler = function (captchaObj)
        {
            captchaObj.appendTo('#captcha');
            captchaObj.onReady(function ()
            {
                document.getElementById('wait').hidden = true;
            });
            document.getElementById('btn-result').addEventListener('click',function(){
                var result = captchaObj.getValidate();
                if (!result)
                {
                    return alert('请完成验证');
                }
                chrome.webview.postMessage(result.geetest_validate + '&' + result.geetest_seccode);
            })
        };

        document.getElementById('btn-gen').addEventListener('click',function(){
            document.getElementById('text').hidden = true;
            document.getElementById('wait').hidden = false;
            initGeetest({
                gt: gt,
                challenge: challenge,
                offline: false, 
                new_captcha: true, 
                product: 'popup', 
                width: '300px',
                https: true
            }, handler);
        })
    </script>
</body>

</html>
";
    }
    public class CaptchaJson
    {
        public int code { get; set; }
        public Data data { get; set; }
        public class Data
        {
            public Result result { get; set; }
            public int type { get; set; }

        }
        public class Result
        {
            public int success { get; set; }
            public string gt { get; set; }
            public string challenge { get; set; }
            public string key { get; set; }
        }
    }
    public class HashJson
    {
        public string hash { get; set; }
        public string key { get; set; }//RSA公钥
    }
    public class LoginJson
    {
        public int code { get; set; }
        public string message { get; set; }
    }
}
