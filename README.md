# WindowsPhoneMatrix

<p align="center">
  <img width="99" height="99" alt="ApplicationIcon" src="https://github.com/user-attachments/assets/d479953b-2b4e-4753-92a7-878ef7f1d2d2" />
</p>

The goal of this project is to develop a Windows Phone native app that can communicate with the synapse/mautrix-meta server combo.
~~The first release is out and the [guide](SETUP.pdf) is up.~~

In my Synaptrix8.1 project I reworked the server setup to be more readable, MD formatted and redid the steps with more security in mind. Even if you are on Windosw Phone 10 I recommend you use that client, it is fully compatible with WP10 and 8.1, has a lot more features and I will still develop it. If you still want to use WindowsPhoneMatrix the server setup guide is available [here](https://github.com/ElisaC04/Synaptrix8.1/blob/main/SETUP.md). Configure only the mautrix-meta bridge as the client is not prepared to handle other bridges :)

To install make sure you install the certificate and dependencies first, which are at the release page.

What I still want to add is:

  -System wide notifications

  -Proper media displaying

On my reddit post somebody told me about [Beeper](https://www.beeper.com/) which is essentially what we accomplish with our server setup, and in theory it makes it easier to configure not only mautrix-meta but every other bridge out there too.
Github user vogtmh create the [Unimatrix](https://github.com/vogtmh/unimatrix) client, which is a Matrix messenger client with built in E2EE support for Windows 10 Mobile.

With this information I dont intend on spending much more time on this app, what I might do is see how one can set up Beeper and then see how you can get that connected to Unimatrix. If I manage then I will document it because it accomplishes what I wanted to in a much better fashion.

What I will work on is porting my app into WP8.1, 8 then 7, 7.5, 7.8 (and maybe even going as far as 6) using the respective WinRT and Silverlight (and winforms) environments. Since what I did here all goes through HTTPS and these systems cant handle E2EE it fits the method I used better.

I will try to get this app into the LiveStore and WUT if I can. If you need help feel free to reach out!
