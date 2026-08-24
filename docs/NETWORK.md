# 재민랭 네트워크 모드

여기서는 재민랭에서 `메가커피`로 파일의 역할을 네트워크 모드로 지정하였을 때 사용되는 구문을 설명합니다.  

> [!NOTE]
> 문법은 [재민랭의 기본 문법](../README.md#문법)을 따릅니다.  
> 또한 여기에 명시되지 않은 키워드는 [재민랭의 기본 키워드](../README.md#코드-작성법)를 따릅니다.

네트워크 모드의 키워드는 아래와 같이 바뀝니다.  
|키워드|기능|
|-----|-----|
|재민|HTTP 요청 보내기|
|러스트|HTTPS 요청 보내기|
|안산|웹서버 만들기|
|팝콘|엔드포인트 등록|
|콜라|정적 파일 서빙|
|엘릭서|파일 저장|

## 코드 작성법

### HTTP 요청 보내기 (재민)

`재민` 키워드로 HTTP 요청을 보낼 수 있습니다.  

|파라메터|비고|
|-|-|
|호스트이름|요청을 보낼 호스트 이름|
|포트번호|요청을 보낼 호스트의 포트 번호|
|페이로드|HTTP 요청 페이로드|
|결과|결과를 저장할 변수 이름|

예제 코드는 아래 [HTTP 요청](#1-http-요청) 참조

### HTTPS 요청 보내기 (러스트)

`러스트` 키워드로 HTTPS 요청을 보낼 수 있습니다.  

|파라메터|비고|
|-|-|
|호스트이름|요청을 보낼 호스트 이름|
|포트번호|요청을 보낼 호스트의 포트 번호|
|페이로드|HTTP 요청 페이로드|
|결과|결과를 저장할 변수 이름|

예제 코드는 아래 [HTTPS 요청](#2-https-요청) 참조

### 웹서버 만들기 (안산)

`안산` 키워드로 웹서버를 만들 수 있습니다.  

|파라메터|비고|
|-|-|
|웹서버 이름|웹서버의 이름|

웹서버를 선언 후, 다른 키워드로 엔드포인트를 등록할 수 있습니다.  

## 예제

### 1. HTTP 요청

```
메가커피,1

그램,payload,"GET / HTTP/1.1\r\n"
그램,payload,+"Host: localhost\r\n"
그램,payload,+"Accept: */*\r\n"
그램,payload,+"Connection: close\r\n"
그램,payload,+"User-Agent: jaeminlang/0.8.0\r\n"
그램,payload,+"\r\n"

재민,"localhost",8080,payload,result

메가커피,0

안산,result.status
안산,"\r\n"
안산,result.body
안산,"\r\n"
안산,result.time
안산,"\r\n"
안산,result.header
```

### 2. HTTPS 요청

```
메가커피,1

그램,payload,"GET / HTTP/1.1\r\n"
그램,payload,+"Host: google.com\r\n"
그램,payload,+"Accept: */*\r\n"
그램,payload,+"Connection: close\r\n"
그램,payload,+"User-Agent: jaeminlang/0.8.0\r\n"
그램,payload,+"\r\n"

러스트,"google.com",443,payload,result

메가커피,0

안산,result.status
안산,"\r\n"
안산,result.body
안산,"\r\n"
안산,result.time
안산,"\r\n"
안산,result.header
```
