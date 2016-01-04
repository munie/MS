// main.js

var http = require("http");
var url = require("url");
var util = require("util");
var net = require("net");
var EncryptSym = require("./EncryptSym");

var tmp = EncryptSym.encode("/center/clientlist");
var clientlist = String.fromCharCode(2) + String.fromCharCode(12 + 128)
	+ String.fromCharCode(tmp.length+4) + String.fromCharCode(0) + tmp;

http.createServer(function(request, response) {
	var pathname = url.parse(request.url).pathname;
	if (pathname != '/center/clientlist') {
		response.writeHead(404, {'Content-Type': 'text/plain'});
		response.end("Unknown request.");
		console.log(request.url);
		return;
	}
	
	var client = net.connect({host: '127.0.0.1', port: 2000}, function() {
		console.log('connected sucsess');
		client.write(clientlist);
	});

	client.on('data', function(data) {
		//console.log(data.toString());
		client.end();
		response.writeHead(200, {'Content-Type': 'application/json;charset=UTF-8'});
		response.end(data);
	});

	client.on('end', function() {
	    console.log('disconnected from server');
	});
	
	//response.writeHead(200, {'Content-Type': 'text/plain'});
	//response.end(util.inspect(url.parse(request.url, true)));
}).listen(2001);