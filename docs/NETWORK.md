# 재민랭 네트워크 모드

여기서는 재민랭에서 `메가커피`로 파일의 역할을 네트워크 모드로 지정하였을 때 사용되는 구문을 설명합니다.  

> [!NOTE]
> 문법은 [재민랭의 기본 문법](../README.md#문법)을 따릅니다.  
> 또한 여기에 명시되지 않은 키워드는 [재민랭의 기본 키워드](../README.md#코드-작성법)를 따릅니다.

네트워크 모드의 키워드는 아래와 같이 바뀝니다.  
|키워드|기능|
|-----|-----|
|재민|HTTP 요청 보내기|
|엘릭서|파일 저장|
|안산|웹서버 만들기|
|팝콘|엔드포인트 등록|
|콜라|정적 파일 서빙|

## 코드 작성법

### 요청 보내기 (재민)

`재민` 키워드로 요청을 보낼 수 있습니다.  

|파라메터|비고|
|-|-|
|페이로드|정보가 들어있는 딕셔너리|
|결과 변수명|결과를 저장할 변수명|

페이로드는 아래와 같아야 합니다.  
|키|값|
|-|-|
|url|요청 보낼 URL|
|method|HTTP 메서드|
|headers|헤더 문자열|
|body|POST, PUT 등 요청에 사용되는 데이터|

결과는 아래와 같이 저장됩니다.  
|키|값|
|-|-|
|status|숫자값|
|time|걸린 시간|
|headers|헤더 딕셔너리|
|json|응답의 딕셔너리 또는 배열|
|form|응답의 formBody 딕셔너리|
|text|응답의 raw text|

> [!CAUTION]
> json, form은 응답 헤더의 `Content-Type`에 따라 저장될 수도, 안될 수도 있습니다.  

### 파일 저장 (엘릭서)

`엘릭서` 키워드로 파일을 다운로드 할 수 있습니다.  
사용법은 `재민` 키워드와 유사합니다.

|파라메터|비고|
|-|-|
|페이로드|정보가 들어있는 딕셔너리|
|파일 이름|저장할 파일 이름|

페이로드는 아래와 같아야 합니다.  
|키|값|
|-|-|
|url|요청 보낼 URL|
|method|HTTP 메서드|
|headers|헤더 문자열|
|body|POST, PUT 등 요청에 사용되는 데이터|

### 웹서버 만들기 (안산)

`안산` 키워드로 웹서버를 만들 수 있습니다.  
서버는 로컬 주소(`127.0.0.1`)에서 실행되며, 프로그램은 서버를 계속 실행하기 위해 종료되지 않습니다.

|파라메터|비고|
|-|-|
|웹서버 이름|웹서버의 이름|
|포트|선택, 기본값은 8080|

웹서버를 선언 후, 다른 키워드로 엔드포인트를 등록할 수 있습니다.  

### 엔드포인트 등록 (팝콘)

`팝콘` 키워드로 재민랭 함수를 HTTP 엔드포인트에 연결할 수 있습니다.

|파라메터|비고|
|-|-|
|웹서버 이름|`안산`으로 만든 서버 이름|
|HTTP 메서드|`GET`, `POST` 등의 메서드, 모든 메서드는 `*`|
|경로|`/hello` 형식의 URL 경로. `{name}`과 `{*name}` 지원|
|함수 이름|요청 딕셔너리를 하나 받고 응답 딕셔너리를 하나 반환하는 함수|

요청 딕셔너리는 아래 값을 포함합니다.

|키|값|
|-|-|
|method|HTTP 메서드|
|path|요청 경로|
|params|동적 경로 파라미터 딕셔너리|
|query|쿼리 문자열 딕셔너리|
|headers|요청 헤더 딕셔너리|
|text|요청 본문 원문|
|json|JSON 요청 본문, Content-Type이 JSON일 때만 저장|
|form|form 요청 본문, Content-Type이 form일 때만 저장|
|remoteAddress|클라이언트 IP 주소|

동적 경로는 한 경로 조각을 받는 `{name}`과 남은 경로 전체를 받는 `{*name}`을 지원합니다.

```jml
팝콘,app,"GET","/users/{id}",getUser
팝콘,app,"GET","/files/{*path}",getFile
```

`/users/42` 요청에서는 `request.params.id`가 `42`가 되고, `/files/images/logo.png` 요청에서는 `request.params.path`가 `images/logo.png`가 됩니다. 정확한 리터럴 경로가 동적 경로보다 우선하며, 일반 경로 파라미터가 catch-all보다 우선합니다.

함수에서 반환하는 응답 딕셔너리는 아래 값을 사용할 수 있습니다.

|키|값|
|-|-|
|status|HTTP 상태 코드, 기본값은 200|
|headers|응답 헤더 딕셔너리|
|contentType|응답 Content-Type|
|text|문자열 응답 본문|
|json|JSON 응답으로 직렬화할 값|
|body|문자열 또는 JSON으로 직렬화할 값|

### 정적 파일 서빙 (콜라)

`콜라` 키워드로 파일 또는 디렉터리를 URL 경로에 연결할 수 있습니다. 디렉터리 경로는 하위 파일을 제공하며, 디렉터리 자체를 요청하면 `index.html`을 찾습니다.

|파라메터|비고|
|-|-|
|웹서버 이름|`안산`으로 만든 서버 이름|
|URL 경로|정적 파일을 제공할 경로|
|파일 경로|파일 또는 디렉터리 경로|

## 예제

### 1. HTTP 요청

```
메가커피,1

그램,headers,"Accept: */*\r\n"
그램,headers,+"User-Agent: jaeminlang/0.8.0"

그램,{payload},url,"https://api.sampleapis.com/coffee/hot?title=Black%20Coffee"
그램,payload.method,"get"
그램,payload.headers,headers

재민,payload,result

메가커피,0

안산,result.status
안산,"\r\n"
```

### 2. 웹서버

```jml
엘릭서,hello,request
그램,{body},message,"Hello World"
그램,{response},status,200
그램,response.json,body
음...,response

엘릭서,getUser,request
그램,{body},id,request.params.id
그램,{response},status,200
그램,response.json,body
음...,response

메가커피,1

안산,app,8080
팝콘,app,"GET","/hello",hello
팝콘,app,"GET","/users/{id}",getUser
콜라,app,"/assets","./public"
```

위 코드를 실행한 뒤 아래 주소로 요청하면 JSON 응답을 받을 수 있습니다.

- `http://127.0.0.1:8080/hello` → `{"message":"Hello World"}`
- `http://127.0.0.1:8080/users/42` → `{"id":"42"}`

서버는 `Ctrl+C`로 종료합니다.

### 3. 파일 다운로드

```
메가커피,1

그램,headers,"Accept: */*\r\n"
그램,headers,+"User-Agent: jaeminlang/0.8.0"

그램,{payload},url,"https://api.sampleapis.com/coffee/hot?title=Black%20Coffee"
그램,payload.method,"get"
그램,payload.headers,headers

재민,payload,result

그램,{imagepayload},url,result.json.0.image
엘릭서,imagepayload,image.png
```
