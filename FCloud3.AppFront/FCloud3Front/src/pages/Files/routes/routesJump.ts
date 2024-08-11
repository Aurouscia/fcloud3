import { useRouter } from "vue-router"
import {  } from "@/models/diff/diffContentTypes"

export function useFilesRoutesJump(){
    const router = useRouter();
    const jumpToDirFromId = (id:number)=>{
        router.push({name:'filesFromId', params:{id}})
    }
    const jumpToDirFromIdRoute = (id:number)=>{
        return {name:'filesFromId', params:{id}}
    }
    const jumpToViewFileItemRoute = (fileItemId:number)=>{
        return {name:'fileItemView', params:{fileItemId}}
    }
    const jumpToRootDir = (urlPathName?:string)=>{
        router.push({name:'files',params:{path:urlPathName}})
    }
    const jumpToDir = (path:string[])=>{
        router.push({name:'files', params:{path}})
    }
    const jumpToHomelessFiles = ()=>{
        router.push({name:'files', params:{path:['homeless-items']}})
    }
    return { jumpToDirFromId, jumpToDirFromIdRoute, jumpToViewFileItemRoute, jumpToRootDir, jumpToDir, jumpToHomelessFiles }
}