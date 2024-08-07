import { useRouter } from "vue-router"
import {  } from "@/models/diff/diffContentTypes"

export function useFilesRoutesJump(){
    const router = useRouter();
    const jumpToDirFromId = (id:number)=>{
        router.push({name:'filesFromId', params:{id}})
    }
    const jumpToRootDir = (urlPathName?:string)=>{
        router.push({name:'files',params:{path:urlPathName}})
    }
    const jumpToDir = (path:string[])=>{
        const joined = path.join('/');
        router.push("/d/"+joined)
    }
    return { jumpToDirFromId, jumpToRootDir, jumpToDir }
}