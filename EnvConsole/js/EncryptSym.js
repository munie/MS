// EncryptSym.js

var crypto = require("crypto");

function BytesToString(bytes) {
	var retval = "";
	for (var i = 0; i < bytes.length; i++)
		retval += String.fromCharCode(bytes[i])
	return retval;
}

var KEY128 = "$MU#ERu{*90Q3,CR";
var IV = BytesToString([0x00, 0x0C, 0x10, 0x00, 0xAB, 0x88, 0x06, 0x25,
            0xBC, 0x92, 0x10, 0x01, 0xCD, 0x88, 0x11, 0x05]);		
			
exports.encode = function (data) {
	var cipher = crypto.createCipheriv('aes-128-cbc', KEY128, IV);
	var retval = cipher.update(data, 'binary', 'base64');
	retval += cipher.final('base64');
	return retval;
}

//module.exports = encode;