<script setup lang="ts">
import { inject, onMounted, provide, ref, watch } from 'vue';
import IndexMini, { IndexColumn } from '../../components/Index/IndexMini.vue';
import { Api } from '../../utils/api';
import { IndexQuery, IndexResult } from '../../components/Index';
import FileDirChild from './FileDirChild.vue';
import { useRouter } from 'vue-router';
import _ from 'lodash';
import SideBar from '../../components/SideBar.vue';
import settingsImg from '../../assets/settings.svg';
import newDirImg from '../../assets/newDir.svg';
import FileDirEdit from './FileDirEdit.vue';
import { FileDir,FileDirSubDir,FileDirItem, FileDirWiki } from '../../models/files/fileDir';
import FileDirItems from './FileDirItems.vue';
import ClipBoard, { ClipBoardItem, ClipBoardItemType, PutEmitCallBack } from '../../components/ClipBoard.vue';
import Functions from '../../components/Functions.vue';
import FileDirCreate from './FileDirCreate.vue';


const props = defineProps<{
    path:string[]|string
}>();
const router = useRouter();
const columns:IndexColumn[] = 
[
    {name:'Name',alias:'名称',canSearch:true,canSetOrder:true},
    {name:'Updated',alias:'上次更新',canSearch:false,canSetOrder:true},
    {name:'ByteCount',alias:'尺寸',canSearch:false,canSetOrder:true},
]

const subDirs = ref<(FileDirSubDir & {showChildren?:boolean|undefined})[]>([]);
const items = ref<FileDirItem[]>([]);
const wikis = ref<FileDirWiki[]>([]);
const thisDirId = ref<number>(0);
const friendlyPath = ref<string[]>([]);
const friendlyPathAncestors = ref<string[]>([]);
const friendlyPathThisName = ref<string>();
//会在OnMounted下一tick被MiniIndex执行，获取thisDirId
const fetchIndex:(q:IndexQuery)=>Promise<IndexResult|undefined>=async(q)=>{
    var p = props.path;
    if(typeof p === 'string'){
        if(p==''){p=[]}
        else{p = [p];}
    }
    const res = await api.fileDir.index(q,p)
    if(res){
        thisDirId.value = res?.ThisDirId || 0;
        renderItems(res.Items);
        renderSubdirs(res.SubDirs);
        renderWikis(res.Wikis);
        friendlyPath.value = res.FriendlyPath;
        setFriendlyPathData();
        hideFn.value = false;
        return res?.SubDirs;
    }
}
//TODO：这三个难看的玩意是不是可以改进一下
function renderItems(i:IndexResult|undefined){
    items.value = [];
    i?.Data?.forEach(r=>{
        items.value.push({
            Id:parseInt(r[0]),
            Name:r[1],
            Updated:r[2],
            OwnerName:r[3],
            ByteCount:parseInt(r[4]),
            Url:r[5]
        })
    })
}
function renderSubdirs(i:IndexResult|undefined){
    subDirs.value = [];
    i?.Data?.forEach(r=>{
        subDirs.value?.push({
            Id: parseInt(r[0]),
            Name:r[1],
            UrlPathName:r[2],
            Updated:r[3],
            OwnerName:r[4],
            ByteCount:parseInt(r[5]),
            FileNumber:parseInt(r[6])
        })
    })
}
function renderWikis(i:IndexResult|undefined){
    wikis.value = [];
    i?.Data?.forEach(r=>{
        wikis.value?.push({
            Id: parseInt(r[0]),
            Name:r[1],
            UrlPathName:r[2],
            Updated:r[3],
            OwnerName:r[4],
        })
    })
}


function jumpToSubDir(name:string){
    var path =  _.concat(pathThis.value,name);
    path = _.filter(path, x=>!!x)
    router.push({name:'files',params:{path}});
}
function jumpToAncestor(idxInChain:number){
    router.push({name:'files',params:{path: _.take(pathThis.value,idxInChain+1)}})
}
function infoUpdated(newInfo:FileDir){
    if(newInfo.UrlPathName && newInfo.UrlPathName!=pathThisName.value){
        var path = _.concat(pathAncestors.value,newInfo.UrlPathName);
        path = _.filter(path, x=>!!x)
        router.replace({name:'files',params:{path}});
    }
    if(newInfo.Name && newInfo.Name!=friendlyPathThisName.value){
        friendlyPathThisName.value = newInfo.Name;
    }
}

const pathAncestors = ref<string[]>([]);
const pathThisName = ref<string>("");
const pathThis = ref<string[]>([]);
const isRoot = ref<boolean>(false);
function setPathData(){
    if(typeof props.path==='string' || props.path.length==0){
        pathAncestors.value = [];
        pathThisName.value = '';
        pathThis.value = [];
        isRoot.value = true;
    }else{
        pathThis.value = _.filter(props.path, x=>!!x)
        pathAncestors.value = _.take(pathThis.value,pathThis.value.length-1)
        pathThisName.value = _.last(pathThis.value)||"";
        isRoot.value = pathThis.value.length==0;
    }
}
function setFriendlyPathData(){
    if(friendlyPath.value.length==0){
        friendlyPath.value = [];
        friendlyPathThisName.value = '';
        friendlyPathAncestors.value = [];
    }else{
        friendlyPath.value = _.filter(friendlyPath.value, x=>!!x)
        friendlyPathAncestors.value = _.take(friendlyPath.value,friendlyPath.value.length-1)
        friendlyPathThisName.value = _.last(friendlyPath.value)||"";
    }
}
const sidebar = ref<InstanceType<typeof SideBar>>();
function startEditDirInfo(){
    sidebar.value?.extend();
}
const newDirSidebar = ref<InstanceType<typeof SideBar>>();
function startCreatingDir(){
    newDirSidebar.value?.extend();
}
function dirCreated(pathUrlName:string){
    newDirSidebar.value?.fold();
    jumpToSubDir(pathUrlName);
}
async function deleteDir(dirId:number){
    const resp = await api.fileDir.delete(dirId);
    if(resp){
        await index.value?.reloadData();
    }
}

const index = ref<InstanceType<typeof IndexMini>>();
var api:Api;
const ok = ref<boolean>(false);
onMounted(async()=>{
    api = inject('api') as Api;
    setPathData();
    index.value?.setPageSizeOverride(isRoot.value?20:1000)
    ok.value = true;//api的inject必须和Index的Mount不在一个tick里，否则里面获取不到fetchIndex
})
watch(props,async(_newVal)=>{
    setPathData();
    sidebar.value?.fold();
    index.value?.setPageSizeOverride(isRoot.value?20:1000)
    await index.value?.reloadData();
});
const hideFn = ref<boolean>(false);
function hideFnUpdate(){
    if(subDirs.value.length==0 || subDirs.value.every(x=>!x.showChildren)){
        hideFn.value = false;
    }else{
        hideFn.value = true;
    }
}

const clip = ref<InstanceType<typeof ClipBoard>>();
provide('clipboard',clip)
function toClipBoard(e:MouseEvent, id:number, name:string, type:ClipBoardItemType){
    clip.value?.insert({
        id:id,
        name:name,
        type:type
    },e)
}
async function clipBoardAction(move:ClipBoardItem[], putEmitCallBack:PutEmitCallBack){
    const fileItemIds = move.filter(x=>x.type=='fileItem').map(x=>x.id);
    const fileDirIds = move.filter(x=>x.type=='fileDir').map(x=>x.id);
    const wikiItemIds = move.filter(x=>x.type=='wikiItem').map(x=>x.id);
    const res = await api.fileDir.putInThings(pathThis.value,fileItemIds,fileDirIds,wikiItemIds);
    if(res){
        const fileItemSuccess:ClipBoardItem[] = res.FileItemSuccess?.map(x=>{return{id:x,type:'fileItem',name:''}})||[]
        const fileDirSuccess:ClipBoardItem[] = res.FileDirSuccess?.map(x=>{return{id:x,type:'fileDir',name:''}})||[]
        const wikiItemSuccess:ClipBoardItem[] = res.WikiItemSuccess?.map(x=>{return{id:x,type:'wikiItem',name:''}})||[]
        console.log(fileItemSuccess,fileDirSuccess,wikiItemSuccess);
        const success = _.concat(fileItemSuccess, fileDirSuccess, wikiItemSuccess)
        console.log(success);
        putEmitCallBack(success,res.FailMsg)
        index.value?.reloadData();
    }
}
</script>

<template>
    <div v-if="ok" class="fileDir">
        <div>
            <div class="ancestors">
                <div>
                    <span @click="jumpToAncestor(-1)">根目录</span>/
                </div>
                <div v-for="a,idx in friendlyPathAncestors">
                    <span @click="jumpToAncestor(idx)">{{ a }}</span>/
                </div> 
            </div>
            <div v-if="thisDirId<0" class="thisName">
                {{ friendlyPathThisName }}
            </div>
            <div v-else-if="friendlyPathThisName && thisDirId>0" class="thisName">
                {{ friendlyPathThisName }}
                <img class="settingsBtn" @click="startEditDirInfo" :src='settingsImg'/>
                <img class="settingsBtn newDirBtn" @click="startCreatingDir" :src='newDirImg'/>
            </div>
            <div v-else-if="thisDirId==0" class="thisName">
                根目录
                <img class="settingsBtn newDirBtn" @click="startCreatingDir" :src='newDirImg'/>
            </div>
        </div>
        <IndexMini ref="index" :fetch-index="fetchIndex" :columns="columns" :display-column-count="1"
            :hide-page="!isRoot" :hide-fn="hideFn">
            <tr v-for="item in subDirs" :key="item.Id">
                <td>
                    <div class="subdir">
                        <div>
                            <div class="foldBtn" v-show="!item.showChildren" @click="item.showChildren=true;hideFnUpdate()" style="color:#999">▶</div>
                            <div class="foldBtn" v-show="item.showChildren" @click="item.showChildren=false;hideFnUpdate()" style="color:black">▼</div>
                            <div class="subdirName" @click="jumpToSubDir(item.UrlPathName)">{{ item.Name }}</div>
                            <Functions :entry-size="20" x-align="left">
                                <button class="minor" @click="toClipBoard($event,item.Id,item.Name,'fileDir')">移动</button>
                                <button class="danger" @click="deleteDir(item.Id)">删除</button>
                            </Functions>
                        </div>
                        <div>
                        </div>
                    </div>
                    <div class="detail" v-if="item.showChildren">
                        <FileDirChild :dir-id="item.Id" :path="_.concat(props.path, item.UrlPathName)" :fetch-from="api.fileDir.index"></FileDirChild>
                    </div>
                </td>
            </tr>
            <tr v-if="items.length>0 || wikis.length>0">
                <td>
                    <FileDirItems :dir-id="thisDirId" :items="items" :wikis="wikis" @need-refresh="index?.reloadData"></FileDirItems>
                </td>
            </tr>
            <tr v-if="subDirs.length==0 && items.length==0 && wikis.length==0">
                <td><div class="emptyDir">空文件夹</div></td>
            </tr>
        </IndexMini>
    </div>    
    <SideBar ref="sidebar">
        <FileDirEdit v-if="thisDirId!=0" :id="thisDirId" :path="pathThis" @info-updated="infoUpdated" @added-new-file="index?.reloadData"></FileDirEdit>
    </SideBar>
    <SideBar ref="newDirSidebar">
        <FileDirCreate :dir-id="thisDirId" :dir-name="friendlyPathThisName||''" @created="dirCreated"></FileDirCreate>
    </SideBar>
    <ClipBoard ref="clip" :current-dir="friendlyPathThisName||'根文件夹'" @put-down="clipBoardAction"></ClipBoard>
</template>

<style scoped>
.items{
    padding-left: 20px;
}
.settingsBtn:hover{
    background-color: #999;
    cursor: pointer;
}
.settingsBtn{
    object-fit: contain;
    width: 20px;
    height: 20px;
    padding: 2px;
    background-color: #bbb;
    border-radius: 5px;
    transition: 0.5s;
}
.newDirBtn{
    width: 18px;
    height: 18px;
    padding: 4px;
}
.ancestors{
    font-size: small;
    color:gray;
    display: flex;
}
.ancestors div span{
    padding: 0px 3px 0px 3px;
}
.ancestors div span:hover{
    text-decoration: underline;
    cursor: pointer;
}
.thisName{
    font-size: 20px;
    margin-top: 5px;
    margin-bottom: 10px;
    user-select: none;
    display: flex;
    flex-direction: row;
    align-items:center;
    gap:5px
}
.emptyDir{
    text-align: left;
    color:#999;
    font-size: small;
}
.detail{
    display: flex;
    flex-direction: column;
    gap:5px;
    padding-bottom: 10px;
    border-left: 1px solid black;
    border-bottom: 1px solid black;
    margin-left: 11px;
    padding-left: 4px;
}
.foldBtn{
    width: 20px;
    overflow: visible;
    cursor: pointer;
    user-select: none;
}
.subdirName{
    text-align: left;
    font-weight: bold;
}
.subdirName:hover{
    text-decoration: underline;
    cursor: pointer;
}
.subdir div{
    display: flex;
    flex-direction: row;
    justify-content: left;
    align-items: center;
    gap:5px
}
.subdir{
    display: flex;
    flex-direction: row;
    justify-content: left;
    gap:20px;
    align-items: center;
    padding: 4px;
}
.fileDir{
    padding-bottom: 200px;
}
</style>