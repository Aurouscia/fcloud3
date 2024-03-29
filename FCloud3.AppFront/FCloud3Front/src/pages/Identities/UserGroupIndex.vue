<script setup lang="ts">
import { inject, onMounted, onUnmounted, ref } from 'vue';
import { Api } from '../../utils/api';
import { UserGroupListResult } from '../../models/identities/userGroup';
import Loading from '../../components/Loading.vue';
import UserGroupDetail from './UserGroupDetail.vue';
import SideBar from '../../components/SideBar.vue';
import { useRouter } from 'vue-router';

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
const sidebar = ref<InstanceType<typeof SideBar>>();
const detail = ref<InstanceType<typeof UserGroupDetail>>();
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

var api:Api
var windowWidth = ref<number>(600);
function getWidth(){
    windowWidth.value = window.innerWidth;
}
function wide(){
    return windowWidth.value > 800;
}
onMounted(async()=>{
    getWidth();
    window.addEventListener('resize',getWidth);
    api = inject('api') as Api;
    await loadData();
    lookingDetail.value = parseInt(props.id||"")||undefined
});
onUnmounted(()=>{
    window.removeEventListener('resize',getWidth);
})

</script>

<template>
    <h1>用户组一览</h1>
    <div class="userGroupIndex">
        <div class="list">
            <div class="search">
                <input v-model="searching" placeholder="搜索用户组名称"/>
                <button class="minor" @click="loadData">搜索</button>
                <button v-show="searching" class="cancel" @click="searching=undefined;loadData()">清空搜索</button>
            </div>
            <table v-if="data">
                <tbody>
                <tr v-if="data.InvitingMe.length>0">
                    <th class="typeHead" colspan="2">邀请我加入的用户组</th>
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
                    <th class="typeHead" colspan="2">我所在的用户组</th>
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
                    <th class="typeHead" colspan="2">本站其他用户组</th>
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
            <UserGroupDetail ref="detail" :id="lookingDetail||0" @need-refresh="loadData">
            </UserGroupDetail>
        </div>
        <SideBar v-else ref="sidebar">
            <UserGroupDetail :id="lookingDetail||0"></UserGroupDetail>
        </SideBar>
    </div>
</template>

<style scoped>
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
    background-color:#999
}
.search{
    height: 40px;
}
.userGroupIndex{
    display: flex;
    flex-direction: row;
    align-items: stretch;
    gap:10px;
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
    max-height: calc(100vh - var(--main-div-margin-top) - 60px);
}
.userGroupIndex .detail{
    flex-grow: 1;
    flex-basis: 250px;
    max-height: calc(100vh - var(--main-div-margin-top) - 60px);
    overflow: auto;
}
</style>