import axios, { Axios } from 'axios'
import {AxiosError} from 'axios'

export type ApiResponse = {
    success: boolean
    data: any
    errmsg: string
}
export type RequestType = "get"|"postForm"|"postRaw";

export type HttpCallBack = (result:"ok"|"warn"|"err",msg:string)=>void
export interface ApiRequestHeader{
    Authorization:string|undefined
}

const storageKey = "fcloudAuthToken"
const defaultFailResp:ApiResponse = {data:undefined,success:false,errmsg:"失败"}

export class HttpClient{
    jwtToken:string|null=null
    httpCallBack:HttpCallBack
    ax:Axios
    constructor(httpCallBack:HttpCallBack){
        this.jwtToken = localStorage.getItem(storageKey);
        this.httpCallBack = httpCallBack;
        this.ax = axios.create({
            baseURL: import.meta.env.VITE_BASEURL,
            validateStatus: (n)=>n < 500
          });
    }
    setToken(token:string){
        this.jwtToken = token;
        localStorage.setItem(storageKey,token);
    }
    clearToken(){
        this.jwtToken = null;
        localStorage.removeItem(storageKey);
    }
    private headers(){
        return {
            Authorization: `Bearer ${this.jwtToken}`
        }
    }
    private showErrToUser(err:AxiosError){
        console.log(err);
        if(err.status){
            const codeText = statusCodeText(err.status);
            if(codeText){
                this.httpCallBack("err", codeText);
                return;
            }
        }
        this.httpCallBack("err","请检查网络连接");
    }
    async request(resource:string,type:RequestType,data?:any,successMsg?:string): Promise<ApiResponse>{
        console.log(`开始发送[${type}]=>[${resource}]`,data)
        var res;
        var timer = setTimeout(()=>{
            this.httpCallBack("warn","请稍等")
        },800)
        try{
            if(type=='get'){
                res = await this.ax.get(
                    resource,
                    {
                        params:data,
                        headers:this.headers(),
                    }
                );
            }else if(type=='postRaw'){
                res = await this.ax.post(
                    resource,
                    data,
                    {
                        headers:this.headers()
                    }
                );
            }else if(type=='postForm'){
                res = await this.ax.postForm(resource,
                    data,
                    {
                        headers:this.headers()
                    }
                );
            }
            clearTimeout(timer);
        }
        catch(ex){
            clearTimeout(timer);
            const err = ex as AxiosError;
            console.log(`[${type}]${resource}失败`,err)
            this.showErrToUser(err);
        }
        if(res){
            const resp = res.data as ApiResponse;
            if(resp.success){
                console.log(`[${type}]${resource}成功`,resp.data)
                if(successMsg){
                    this.httpCallBack('ok',successMsg)
                }
            }
            if(!resp.success){
                console.log(`[${type}]${resource}失败`,resp.errmsg||res.statusText);
                if(resp.errmsg){
                    this.httpCallBack('err',resp.errmsg);
                }else{
                    const codeText = statusCodeText(res.status);
                    this.httpCallBack('err',codeText||"未知错误")
                }
            }
            return resp;
        }else{
            return defaultFailResp;
        }
    }
}

export function statusCodeText(code:number|undefined|null){
    if(code == 401){
        return "请登录";
    }
    if(code == 403){
        return "无权限";
    }
    if(code||0 >= 500){
        return "服务器未知错误";
    }
    return undefined;
}