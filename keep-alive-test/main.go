package main

import (
	"fmt"
	"io"
	"io/ioutil"
	"net"
	"net/http"
	"net/url"
	"time"
)

func main() {
	proxyUrl, _ := url.Parse("http://127.0.0.1:18811")

	d := net.Dialer{
		Timeout:   30 * time.Second,
		KeepAlive: 30 * time.Second,
	}

	c := http.Client{
		Transport: &http.Transport{
			Dial: func(network, addr string) (net.Conn, error) {
				fmt.Printf("Dial : %s %s\n", network, addr)

				return d.Dial(network, addr)
			},
			Proxy: http.ProxyURL(proxyUrl),
			ProxyConnectHeader: http.Header{
				"Proxy-Authorization": []string{"Basic MTIzOjEyMw=="},
				"Connection":          []string{"keep-alive"},
			},
		},
		CheckRedirect: func(req *http.Request, via []*http.Request) error {
			return http.ErrUseLastResponse
		},
		Timeout: 30 * time.Second,
	}

	for {
		req, _ := http.NewRequest("GET", "https://ifconfig.io/ip", nil)
		req.Header.Set("Connection", "keep-alive")
		req.SetBasicAuth("123", "123")
		req.Header.Set("Proxy-Authorization", req.Header.Get("Authorization"))
		req.Header.Del("Authorization")

		/**
		dump, _ := httputil.DumpRequest(req, false)
		fmt.Println(string(dump))
		*/

		res, err := c.Do(req)
		//res.Body.Close()

		if err == nil {
			//dump, _ := httputil.DumpResponse(res, true)
			//fmt.Println(string(dump))
			fmt.Println(res.StatusCode)
			io.Copy(ioutil.Discard, res.Body)
		} else {
			fmt.Println(err)
		}

		time.Sleep(1 * time.Second)
	}
}
