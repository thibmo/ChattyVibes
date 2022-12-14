using ChattyVibes.Events;
using ST.Library.UI.NodeEditor;
using System;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;

namespace ChattyVibes.Nodes.EventNode.TwitchNode
{
    [STNode("/Events/Twitch", "LauraRozier", "", "", "Twitch OnGiftSub event node")]
    internal sealed class OnGiftSubEventNode : EventNode
    {
        private STNodeOption m_op_Channel_out;
        private STNodeOption m_op_DisplayName_out;
        private STNodeOption m_op_Id_out;
        private STNodeOption m_op_IsAnonymous_out;
        private STNodeOption m_op_IsModerator_out;
        private STNodeOption m_op_IsSubscriber_out;
        private STNodeOption m_op_IsTurbo_out;
        private STNodeOption m_op_MsgId_out;
        private STNodeOption m_op_Months_out;
        private STNodeOption m_op_MultiMonthGiftDuration_out;
        private STNodeOption m_op_RecipientDisplayName_out;
        private STNodeOption m_op_SubPlan_out;
        private STNodeOption m_op_SystemMsg_out;
        private STNodeOption m_op_UserId_out;
        private STNodeOption m_op_TmiSentTs_out;

        protected override void BindEvent()
        {
            ((TwitchOnGiftSubEvent)MainForm.EventFactory.GetEvent(EventType.TwitchOnGiftSub)).RaiseEvent +=
                OnEventNode_RaiseEvent;
        }

        protected override void UnbindEvent()
        {
            ((TwitchOnGiftSubEvent)MainForm.EventFactory.GetEvent(EventType.TwitchOnGiftSub)).RaiseEvent -=
                OnEventNode_RaiseEvent;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            Title = "On Gifted Sub";

            m_op_Channel_out = OutputOptions.Add("Channel", typeof(string), false);
            m_op_DisplayName_out = OutputOptions.Add("Display Name", typeof(string), false);
            m_op_Id_out = OutputOptions.Add("ID", typeof(string), false);
            m_op_IsAnonymous_out = OutputOptions.Add("Is Anonymous", typeof(bool), false);
            m_op_IsModerator_out = OutputOptions.Add("Is Moderator", typeof(bool), false);
            m_op_IsSubscriber_out = OutputOptions.Add("Is Subscriber", typeof(bool), false);
            m_op_IsTurbo_out = OutputOptions.Add("Is Turbo", typeof(bool), false);
            m_op_MsgId_out = OutputOptions.Add("Message ID", typeof(string), false);
            m_op_Months_out = OutputOptions.Add("Months", typeof(int), false);
            m_op_MultiMonthGiftDuration_out = OutputOptions.Add("Multi-month Gift Duration", typeof(int), false);
            m_op_RecipientDisplayName_out = OutputOptions.Add("Recipient Display Name", typeof(string), false);
            m_op_SubPlan_out = OutputOptions.Add("SubscriptionPlan", typeof(SubscriptionPlan), false);
            m_op_SystemMsg_out = OutputOptions.Add("System Message", typeof(string), false);
            m_op_UserId_out = OutputOptions.Add("UserId", typeof(string), false);
            m_op_TmiSentTs_out = OutputOptions.Add("Timestamp", typeof(DateTime), false);
        }

        private void OnEventNode_RaiseEvent(object sender, OnGiftedSubscriptionArgs e)
        {
            m_op_Channel_out.TransferData(e.Channel);
            m_op_DisplayName_out.TransferData(e.GiftedSubscription.DisplayName);
            m_op_Id_out.TransferData(e.GiftedSubscription.Id);
            m_op_IsAnonymous_out.TransferData(e.GiftedSubscription.IsAnonymous);
            m_op_IsModerator_out.TransferData(e.GiftedSubscription.IsModerator);
            m_op_IsSubscriber_out.TransferData(e.GiftedSubscription.IsSubscriber);
            m_op_IsTurbo_out.TransferData(e.GiftedSubscription.IsTurbo);
            m_op_MsgId_out.TransferData(e.GiftedSubscription.MsgId);

            if (!int.TryParse(e.GiftedSubscription.MsgParamMonths, out int months))
                months = 0;

            m_op_Months_out.TransferData(months);

            if (!int.TryParse(e.GiftedSubscription.MsgParamMultiMonthGiftDuration, out int mmgDuration))
                mmgDuration = 0;

            m_op_MultiMonthGiftDuration_out.TransferData(mmgDuration);
            m_op_RecipientDisplayName_out.TransferData(e.GiftedSubscription.MsgParamRecipientDisplayName);
            m_op_SubPlan_out.TransferData(e.GiftedSubscription.MsgParamSubPlan);
            m_op_SystemMsg_out.TransferData(e.GiftedSubscription.SystemMsg);
            m_op_UserId_out.TransferData(e.GiftedSubscription.UserId);

            long timestamp = long.Parse(e.GiftedSubscription.TmiSentTs);
            DateTime dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
            m_op_TmiSentTs_out.TransferData(dt);

            Trigger();
        }
    }
}
