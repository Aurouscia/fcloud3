<script setup lang="ts">
import { inject, onMounted, onUnmounted, ref, useTemplateRef } from 'vue';
import { Api } from '@/utils/com/api';
import { UserGroupListResult } from '@/models/identities/userGroup';
import Loading from '@/components/Loading.vue';
import UserGroupDetail from './UserGroupDetail.vue';
import SideBar from '@/components/SideBar.vue';
import { useRouter } from 'vue-router';
import { recoverTitle, setTitleTo } from '@/utils/titleSetter';

const props = defineProps<{
    id?:string
}>();
const router = useRouter();

const data = ref<UserGroupListResult>();
const searching = ref<string>();
async function loadData(){
    var timer = setTimeout(()=>{data.value=undefined},500);
    const res = await api.identites.userGroup.getList(searching.value);
    if(res){
        clearTimeout(timer);
        data.value = res;
    }
}

const lookingDetail = ref<number>();
const sidebar = useTemplateRef('sidebar')
const detail = useTemplateRef('detail')
function lookDetail(id:number){
    lookingDetail.value = id;
    router.replace({name:'userGroup',params:{id:id}})
    sidebar.value?.extend();
}
async function answerInvitation(id:number,accept:boolean){
    const res = await api.identites.userGroup.answerInvitation(id,accept);
    if(res){
        await loadData();
        await detail.value?.loadData();
    }
}
function onDissolved(){
    lookingDetail.value = undefined;
    router.replace({name:'userGroup'});
}


const createSideber = useTemplateRef('createSideber')
const creatingName = ref<string>();
async function create(){
    if(!creatingName.value){return;}
    const resp = await api.identites.userGroup.create(creatingName.value);
    if(resp){
        await loadData();
        creatingName.value = "";
        createSideber.value?.fold();
    }
}

var api:Api
var windowWidth = ref<number>(600);
function getWidth(){
    windowWidth.value = window.innerWidth;
}
function wide(){
    return windowWidth.value > 800;
}
onMounted(async()=>{
    setTitleTo('用户组一览')
    getWidth();
    window.addEventListener('resize',getWidth);
    api = inject('api') as Api;
    await loadData();
    lookingDetail.value = parseInt(props.id||"")||undefined
    if(lookingDetail.value !== undefined){
        sidebar.value?.extend();
    }
});
onUnmounted(()=>{
    recoverTitle()
    window.removeEventListener('resize',getWidth);
})

</script>

<template>
    <h1>
        用户组一览
        <button @click="createSideber?.extend">新建组</button>
    </h1>
    <div class="userGroupIndex">
        <div class="list">
            <div class="search">
                <input v-model="searching" placeholder="搜索用户组名称" @blur="loadData"/>
            </div>
            <table v-if="data">
                <tbody>
                <tr v-if="data.InvitingMe.length>0">
                    <td class="typeHead" colspan="2">邀请我加入的用户组</td>
                </tr>
                <tr v-for="g in data.InvitingMe" :class="{selected:lookingDetail==g.Id}" :key="g.Id">
                    <td>
                        <div class="groupName" @click="lookDetail(g.Id)">
                            {{ g.Name }}
                        </div>
                        <div class="acceptBtns">
                            <button class="ok" @click="answerInvitation(g.Id, true)">接受</button>
                            <button class="cancel" @click="answerInvitation(g.Id, false)">拒绝</button>
                        </div>
                    </td>
                    <td>{{ g.MemberCount }}人</td>
                </tr>
                <tr v-if="data.MeIn.length>0">
                    <td class="typeHead" colspan="2">我所在的用户组</td>
                </tr>
                <tr v-for="g in data.MeIn" :class="{selected:lookingDetail==g.Id}" :key="g.Id">
                    <td>
                        <div class="groupName" @click="lookDetail(g.Id)">
                            {{ g.Name }}
                        </div>
                    </td>
                    <td>{{ g.MemberCount }}人</td>
                </tr>
                <tr v-if="data.Others.length>0">
                    <td class="typeHead" colspan="2">本站其他用户组</td>
                </tr>
                <tr v-for="g in data.Others" :class="{selected:lookingDetail==g.Id}" :key="g.Id">
                    <td>
                        <div class="groupName" @click="lookDetail(g.Id)">
                            {{ g.Name }}
                        </div>
                    </td>
                    <td class="memberCount">{{ g.MemberCount }}人</td>
                </tr>
                </tbody>
            </table>
            <Loading v-else></Loading>
        </div>
        <div v-if="wide()" class="detail">
            <UserGroupDetail ref="detail" :id="lookingDetail||0" @need-refresh="loadData" @dissolved="onDissolved">
            </UserGroupDetail>
        </div>
        <SideBar v-else ref="sidebar">
            <UserGroupDetail :id="lookingDetail||0" @need-refresh="loadData" @dissolved="onDissolved"></UserGroupDetail>
        </SideBar>
    </div>
    <SideBar ref="createSideber">
        <h1>新建用户组</h1>
        <table><tbody>
            <tr>
                <td>名称</td>
                <td>
                    <input v-model="creatingName" placeholder="组名称(必填)"/>
                </td>
            </tr>
            <tr>
                <td colspan="2">
                    <button @click="create">新建</button>
                </td>
            </tr>
        </tbody></table>
    </SideBar>
</template>

<style scoped lang="scss">
@use '@/styles/globalValues';
$user-group-items-height: calc(globalValues.$body-height - 80px);

h1{
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding-bottom: 5px;
    button{
        font-size: medium;
    }
}
.acceptBtns button{
    margin-top: 5px;
    border: 2px solid white
}
.selected td{
    background-color: cornflowerblue;
    color:white
}
.memberCount{
    white-space: nowrap;
}
.groupName{
    min-width: 200px;
    text-align: center;
}
.groupName:hover{
    text-decoration: underline;
    font-weight: bold;
    cursor: pointer;
}
.typeHead{
    font-weight: bold;
    color: white;
    background-color:#999
}
.search{
    height: 40px;
    text-align: center;
}
.userGroupIndex{
    display: flex;
    flex-direction: row;
    align-items: stretch;
    flex-wrap: wrap;
}
.userGroupIndex .list{
    flex-grow: 1;
    width: 250px;
    flex-basis: 250px;
    display: flex;
    flex-direction: column;
    align-items: stretch;
    min-width: 310px;
    gap:5px;
    overflow: auto;
    max-height: $user-group-items-height;
}
.userGroupIndex .detail{
    margin-left: 10px;
    flex-grow: 1;
    flex-basis: 250px;
    max-height: $user-group-items-height;
    overflow: auto;
}
</style>